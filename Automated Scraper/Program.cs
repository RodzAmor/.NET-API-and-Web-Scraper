using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Odbc;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Policy;
using Newtonsoft.Json.Linq;
using System.Web.UI.WebControls;
using System.Collections;
using System.Security.Cryptography;
using System.Data;
using System.Net.Mail;
using System.Net;
using System.IO;
using System.Data.SqlClient;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.Server;
using static System.Net.Mime.MediaTypeNames;

public static class Globals
{
    public static List<string> LOGS = new List<string>(); // Modifiable
}

namespace ModoWindowsService
{
    internal class Program
    {
        private static List<string> message = new List<string>(); // List of message for each mismatch found
        private static int numErrors = 0;

        private static void log(string logLine) { 
            Globals.LOGS.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ": "+ logLine); 
        }
        private void debug(string logLine) { 
            Console.WriteLine("DEBUGGING - " + logLine); 
        }


        private static void CheckDiningHours() {
            // Scraping for restaurant data to see if there are any discrepencies between the database and online sources
            // If there are any mismatches found, it sends an email.

            string[] urls = { 
                "https://dining.umd.edu/hours-locations/cafes",
                "http://www.example.com"
            };

            message.Add("Automated scan performed to find errors between the displayed dining hours in ModoLabs and the actual dining hours.\n");
            
            //message.Add(@"<style>
            //         tr, td, th {
            //          border:1px solid black;
            //            padding: 10px;
            //         }

            //         tr:nth-child(1) {
            //          background-color: #fcc23f;
            //            font-weight: bold;
            //         }

            //         table {
            //         border-collapse: collapse; 
            //            text-align: center;
            //         }
            //         </style>");

            foreach(string url in urls) {
                DataTable scrapedDataTable = ScrapeSite(url);
                DataTable normalDataTable = getDiningHours();
                DataTable modifiedHours = getModifiedHours();

                message.AddRange(CompareTables(scrapedDataTable, normalDataTable, modifiedHours));
            }

            // Test email in the event of no errors
            
            if (numErrors == 0) {
                // This means no errors found
                // If you don't want to send an email at all, just comment this out
                message.Clear();
                message.Add("No errors found between ModoLabs and website dining hours.");
                sendEmail(message);
            } else {
                message.Insert(1, "<br>" + numErrors.ToString() + " errors found for the current week. \n<br>");
                sendEmail(message);
            }

        }

        // Function handles the different types of websites and redirects to the corresponding function
        // Every website should return a dataframe so it can be processed by CompareTables()
        // However, CompareTables() will likely need to be modified because it only works for tables that are formatted like the scraped dining hours website
        private static DataTable ScrapeSite(string url) {
            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(url);
            string text = doc.Text;

            Uri siteUri = new Uri(url);
            string host = siteUri.Host;
            string path = siteUri.AbsolutePath;

            // At the moment we don't have any sites to scrape other than the dining site
            // However, this can be used to scrape different websites
            switch(host) {
                // Dining umd site is scraped differently
                case "dining.umd.edu":
                    return DiningDataTable();
                default:
                    break;
            }

            return new DataTable();
        }

        /* This function is made specifically for the dining.umd.edu site
         * Since the site uses json to populate the tables, we cannot scrape directly from the html
         * Therefore, we have to parse the same json data source they do and perform our own operations
         * Data ends at 9/3/2023 so it will likely need to be changed in the future.
         * 
         * returns datatable
         */
        private static DataTable DiningDataTable() {
            string url = "https://docs.google.com/spreadsheets/d/1XppW_tfjvk_PlKaE4gXF2TtIUiB_iyQguU-VFA4Fh3g/gviz/tq?gid=2021515491"; // Hard coded spreadsheet
            JObject json;

            using (WebClient wc = new WebClient()) {
                string text = wc.DownloadString(url);
                // We are removing the first 47 and the last 2 characters of the api
                text = text.Substring(47, text.Length - 49);
                json = JObject.Parse(text);
            }

            JToken rows = json["table"]["rows"];
            //JToken cols = json["table"]["cols"]; // cols are not relevant here

            JToken dateJson = rows[0].First().First();

            Dictionary<string, List<string>> venues = new Dictionary<string, List<string>>();
            List<string> dates = new List<string>();

            foreach (JToken date in dateJson.Skip(1)) {
                dates.Add(date["v"].ToString());
            }

            foreach(JToken venue in rows.Skip(1)) {
                string venueName = venue.First().First().First()["v"].ToString();
                venues.Add(venueName, new List<string>());

                foreach(JToken date in venue.First().First().Skip(1)) {
                    venues[venueName].Add(date["v"].ToString());
                }

            }
            
            DataTable dt = new DataTable();

            dt.Columns.Add("venue");

            foreach (string date in dates) {
                dt.Columns.Add(date);
            }

            

            foreach(KeyValuePair<string, List<string>> v in venues) {
                DataRow venueRow = dt.NewRow();
                venueRow[0] = v.Key;

                for(int i = 1; i < v.Value.Count; i++) {
                    venueRow[i] = v.Value[i].ToString();
                }

                dt.Rows.Add(venueRow);
            }

            //printDataTable(dt);

            return dt;
        }


        /* Important notes:
         * The reason why I chose to use switch for scraped rows is because the scraped datatable can only be 1 value whereas
         * the other tables have multiple different values
         */
        private static List<string> CompareTables(DataTable scraped, DataTable normal, DataTable modified) {
            // Compare datatables and check if there are any mismatches
            // If mismatches are found, add a line to the message
            List<string> message = new List<string>();
            Dictionary<string, int> venueIds = getScraperIds();

            //test.Clear();
            //test.Add("Program did not crash at point 1.");
            //sendEmail(test);
            // Success!

            //Dictionary<string, int> venueIds = new Dictionary<string, int>();

            // Prepopulate dictionary with relevant indexes in the MySQL server
            // By doing so, we can change the ordering of the columns without breaking the code
            Dictionary<string, int> hoursIndexes = new Dictionary<string, int>() {
                { "sun_open", normal.Columns["sun_open"].Ordinal },
                { "sun_close", normal.Columns["sun_close"].Ordinal },
                { "mon_open", normal.Columns["mon_open"].Ordinal },
                { "mon_close", normal.Columns["mon_close"].Ordinal },
                { "tues_open", normal.Columns["tues_open"].Ordinal },
                { "tues_close", normal.Columns["tues_close"].Ordinal },
                { "wed_open", normal.Columns["wed_open"].Ordinal },
                { "wed_close", normal.Columns["wed_close"].Ordinal },
                { "thurs_open", normal.Columns["thurs_open"].Ordinal },
                { "thurs_close", normal.Columns["thurs_close"].Ordinal },
                { "fri_open", normal.Columns["fri_open"].Ordinal },
                { "fri_close", normal.Columns["fri_close"].Ordinal },
                { "sat_open", normal.Columns["sat_open"].Ordinal },
                { "sat_close", normal.Columns["sat_close"].Ordinal },
            };

            Dictionary<string, int> openIndexes = new Dictionary<string, int>() {
                { "open_sun", normal.Columns["open_sun"].Ordinal },
                { "open_mon", normal.Columns["open_mon"].Ordinal },
                { "open_tues", normal.Columns["open_tues"].Ordinal },
                { "open_wed", normal.Columns["open_wed"].Ordinal },
                { "open_thurs", normal.Columns["open_thurs"].Ordinal },
                { "open_fri", normal.Columns["open_fri"].Ordinal },
                { "open_sat", normal.Columns["open_sat"].Ordinal },
            };

            Dictionary<string, int> tbdIndexes = new Dictionary<string, int>() {
                { "sun_is_tbd", normal.Columns["sun_is_tbd"].Ordinal },
                { "mon_is_tbd", normal.Columns["mon_is_tbd"].Ordinal },
                { "tues_is_tbd", normal.Columns["tues_is_tbd"].Ordinal },
                { "wed_is_tbd", normal.Columns["wed_is_tbd"].Ordinal },
                { "thurs_is_tbd", normal.Columns["thurs_is_tbd"].Ordinal },
                { "fri_is_tbd", normal.Columns["fri_is_tbd"].Ordinal },
                { "sat_is_tbd", normal.Columns["sat_is_tbd"].Ordinal },
            };

            Dictionary<DayOfWeek, string> tableLookupHelper = new Dictionary<DayOfWeek, string>() {
                { DayOfWeek.Sunday, "sun" },
                { DayOfWeek.Monday, "mon" },
                { DayOfWeek.Tuesday, "tues" },
                { DayOfWeek.Wednesday, "wed" },
                { DayOfWeek.Thursday, "thurs" },
                { DayOfWeek.Friday, "fri" },
                { DayOfWeek.Saturday, "sat" },
            };


            // Made obsolete by the Scraper Ids table
            // Complete the dictionary for venue id and name
            //foreach (DataRow scrapedRow in scraped.Rows) {
            //    string venueName = scrapedRow[0].ToString();

            //    foreach(DataRow normalRow in normal.Rows) {
            //        if(Regex.Replace(normalRow[1].ToString(), @"[^A-Za-z0-9]+", "") == Regex.Replace(venueName, @"[^A-Za-z0-9]+", "")) {
            //            venueIds[venueName] = (int)normalRow[0];
            //        }
            //    }
            //}

            foreach (DataRow scrapedRow in scraped.Rows) {
                // Get the DateTime of the Sunday of the current week
                DateTime day = getStartDate();
                
                string venueName = scrapedRow[0].ToString();
                StringBuilder tableRow = new StringBuilder();
                StringBuilder modifiedMessage = new StringBuilder();
                List<string> submessage = new List<string>();

                // Skip it if the sql server does not have the venue included in the tables
                if(!venueIds.ContainsKey(venueName)) {
                    message.Add("An error occured for the venue " + venueName + ": Could not find a matching name key in the web scraper ids SQL table.");


                    // Debugging lines
                    //message.Add("If the email has this line, there is a problem. Type 1");
                    //message.Add(venueName + " hex:");

                    //char[] hex = venueName.ToCharArray();
                    //StringBuilder hexCode = new StringBuilder();
                    //foreach(char letter in hex) {
                    //    int value = Convert.ToInt32(letter);
                    //    hexCode.Append(value.ToString() + " ");
                    //}

                    //message.Add(hexCode.ToString());

                    //foreach (KeyValuePair<string, int> kvp in venueIds) {
                    //    message.Add(kvp.Key + ": " + kvp.Value.ToString());

                    //    char[] values = kvp.Key.ToCharArray();
                    //    StringBuilder hexCode2 = new StringBuilder();
                    //    foreach (char letter in values) {        
                    //        int value = Convert.ToInt32(letter);
                    //        hexCode2.Append(value.ToString() + " ");
                    //    }
                    //    message.Add(hexCode2.ToString());
                    //    message.Add("--------");
                    //}
                    
                    continue;
                }


                StringBuilder headerRow = new StringBuilder();
                submessage.Add("<h2>" + venueName + "</h2>");
                headerRow.Append("<tr*><th>Day</th><th>Date</th><th>Modo</th><th>Website</th><th>Status</th></tr>");

                DataRow[] relevantRows = normal.Select("dining_hours_id = " + venueIds[venueName]);
                

                //message.Add("");
                if (relevantRows.Length == 0) {
                    message.Add("An error occured for the venue " + venueName + ": The venue was not found in the server.");
                    continue;
                }

                DataRow venueRow = relevantRows[0];
                
                // Proceed with checking normal hours
                int pos = scraped.Columns[(day).ToString("%M/d/yyyy")].Ordinal;

                // We need weekday since the scraped data columns are based on date (M/dd/yyyy), however, handling the different situations will use the dictionary
                // The dictionary is only used for the normal table lookups
                
                
                foreach (DayOfWeek weekDay in tableLookupHelper.Keys) {

                    // If it is modified hour, then we should skip 
                    if(!checkModifiedHours(venueIds[venueName], day.AddDays((int)weekDay), scrapedRow, modifiedMessage)) {
                        tableRow.Append("<tr>");
                        tableRow.Append("<td>" + (day.AddDays((int)weekDay)).DayOfWeek.ToString() + "</td>");
                        tableRow.Append("<td>" + (day.AddDays((int)weekDay)).ToString("%M/d/yyyy") + "</td>");
                        
                        try {
                            switch(scrapedRow[pos + (int)weekDay]) {
                                case "Closed":
                                    if(!venueRow[openIndexes["open_" + tableLookupHelper[weekDay]]].ToString().Equals("0")) {
                                        tableRow.Append("<td>Open</td>");
                                        tableRow.Append("<td>Closed</td>");
                                        tableRow.Append("<td><span style=\"color: #dc3545\">Incorrect</span></td>");
                                        numErrors++;
                                    } else {
                                        tableRow.Append("<td></td>");
                                        tableRow.Append("<td></td>");
                                        tableRow.Append("<td><span style=\"color: #28a745\">Correct</span></td>");
                                    }
                                    break;
                                case "TBD":
                                    if(!venueRow[tbdIndexes[tableLookupHelper[weekDay] + "_is_tbd"]].ToString().Equals("0")) {
                                        tableRow.Append("<td>Not TBD</td>");
                                        tableRow.Append("<td>TBD</td>");
                                        tableRow.Append("<td><span style=\"color: #dc3545\">Incorrect</span></td>");
                                        numErrors++;
                                    } else {
                                        tableRow.Append("<td></td>");
                                        tableRow.Append("<td></td>");
                                        tableRow.Append("<td><span style=\"color: #28a745\">Correct</span></td>");
                                    }
                                    break;
                                default:
                                    string[] hours = scrapedRow[pos + (int)weekDay].ToString().Split('-');
                                    string scrapeOpen = Convert.ToDateTime(hours[0]).ToString("h:mm tt");
                                    string scrapeClose = Convert.ToDateTime(hours[1]).ToString("h:mm tt");

                                    string normalOpen = Convert.ToDateTime(venueRow[hoursIndexes[tableLookupHelper[weekDay] + "_open"]].ToString()).ToString("h:mm tt");
                                    string normalClose = Convert.ToDateTime(venueRow[hoursIndexes[tableLookupHelper[weekDay] + "_close"]].ToString()).ToString("h:mm tt");

                                    if(venueRow[openIndexes["open_" + tableLookupHelper[weekDay]]].ToString().Equals("0")) {
                                        tableRow.Append("<td>Open</td>");
                                        tableRow.Append("<td>" + scrapeOpen + " to " + scrapeClose + "</td>");
                                        tableRow.Append("<td><span style=\"color: #dc3545\">Incorrect</span></td>");
                                        numErrors++;
                                    } else if(venueRow[tbdIndexes[tableLookupHelper[weekDay] + "_is_tbd"]].ToString().Equals("1")) {
                                        tableRow.Append("<td>TBD</td>");
                                        tableRow.Append("<td>" + scrapeOpen + " to " + scrapeClose + "</td>");
                                        tableRow.Append("<td><span style=\"color: #dc3545\">Incorrect</span></td>");
                                        numErrors++;
                                    } else if(scrapeOpen != normalOpen || scrapeClose != normalClose) {
                                        tableRow.Append("<td>" + normalOpen + " to " + normalClose + "</td>");
                                        tableRow.Append("<td>" + scrapeOpen + " to " + scrapeClose + "</td>");
                                        tableRow.Append("<td><span style=\"color: #dc3545\">Incorrect</span></td>");
                                        numErrors++;
                                    } else {
                                        tableRow.Append("<td></td>");
                                        tableRow.Append("<td></td>");
                                        tableRow.Append("<td><span style=\"color: #28a745\">Correct</span></td>");
                                    }

                                    break;
                            }
                        } catch (Exception e) {
                            message.Add("Exception occurs in the switch statement");
                            message.Add(e.Message);

                            //List<string> test2 = new List<string>();
                            //test2.Clear();
                            //test2.Add(e.Message);
                            //sendEmail(test2);
                            numErrors += 1;
                        }

                        tableRow.Append("</tr>");
                    } else {
                        tableRow.Append("<tr>");
                        tableRow.Append("<td>" + (day.AddDays((int)weekDay)).DayOfWeek.ToString() + "</td>");
                        tableRow.Append("<td>" + (day.AddDays((int)weekDay)).ToString("%M/d/yyyy") + "</td>");
                        tableRow.Append("<td>See Modified</td>");
                        tableRow.Append("<td>See Modified</td>");
                        tableRow.Append("<td></td>");
                    }
                }

                submessage.Add("<table>" + headerRow.ToString() + tableRow.ToString() + "</table>");

                if (modifiedMessage.Length != 0) {
                    StringBuilder modifiedHeader = new StringBuilder();
                    submessage.Add("<h3>Modified Hours</h3>");
                    modifiedHeader.Append("<tr*><th>Day</th><th>Date</th><th>Modo</th><th>Website</th><th>Status</th></tr>");
                    submessage.Add("<table>" + modifiedHeader.ToString() + modifiedMessage.ToString() + "</table>");
                }
                submessage.Add("");
                message.AddRange(submessage);
            }
            

            return message;


            // Made this a local function so that I don't have to copy all the parameters
            // It's easier and less overhead
            bool checkModifiedHours(int venueId, DateTime date, DataRow scrapedRow, StringBuilder modifiedMessage) {
                DataRow[] relevantRows = modified.Select("dining_hours_id = " + venueId.ToString());
                bool isModified = false;

                foreach(DataRow modifiedRow in relevantRows) {
                    // If their dates match
                    if(DateTime.Equals(date.Date, Convert.ToDateTime(modifiedRow[modified.Columns["date"].Ordinal]).Date)) {
                        isModified = true;
                        
                        // If the scraped data column doesn't contain the date, then it can't check anything so we continue
                        bool valid = scraped.Columns.Contains(date.ToString("%M/d/yyyy"));
                        int pos;

                        if(valid) {
                            pos = scraped.Columns[date.ToString("%M/d/yyyy")].Ordinal;
                        } else {
                            continue;
                        }

                        modifiedMessage.Append("<tr>");
                        modifiedMessage.Append("<td>" + date.DayOfWeek.ToString() + "</td>");
                        modifiedMessage.Append("<td>" + date.ToString("%M/d/yyyy") + "</td>");

                        switch(scrapedRow[pos]) {
                            case "Closed":
                                if(!modifiedRow["modified_is_open"].ToString().Equals("0")) {
                                    modifiedMessage.Append("<td>Open</td>");
                                    modifiedMessage.Append("<td>Closed</td>");
                                    modifiedMessage.Append("<td><span style=\"color: #dc3545\">Incorrect</span></td>");
                                    numErrors++;
                                } else {
                                    modifiedMessage.Append("<td></td>");
                                    modifiedMessage.Append("<td></td>");
                                    modifiedMessage.Append("<td><span style=\"color: #28a745\">Correct</span></td>");
                                }
                                break;

                            case "TBD":
                                if(!modifiedRow["modified_is_tbd"].ToString().Equals("0")) {
                                    modifiedMessage.Append("<td>Not TBD</td>");
                                    modifiedMessage.Append("<td>TBD</td>");
                                    modifiedMessage.Append("<td><span style=\"color: #dc3545\">Incorrect</span></td>");
                                    numErrors++;
                                } else {
                                    modifiedMessage.Append("<td></td>");
                                    modifiedMessage.Append("<td></td>");
                                    modifiedMessage.Append("<td><span style=\"color: #28a745\">Correct</span></td>");
                                }
                                break;

                            default:
                                string[] hours = scrapedRow[pos].ToString().Split('-');
                                string scrapeOpen = Convert.ToDateTime(hours[0]).ToString("h:mm tt");
                                string scrapeClose = Convert.ToDateTime(hours[1]).ToString("h:mm tt");

                                string modifiedOpen = Convert.ToDateTime(modifiedRow["modified_open"].ToString()).ToString("h:mm tt");
                                string modifiedClose = Convert.ToDateTime(modifiedRow["modified_close"].ToString()).ToString("h:mm tt");

                                if(modifiedRow["modified_is_open"].ToString().Equals("0")) {
                                    modifiedMessage.Append("<td>Closed</td>");
                                    modifiedMessage.Append("<td>" + scrapeOpen + " to " + scrapeClose + "</td>");
                                    modifiedMessage.Append("<td><span style=\"color: #dc3545\">Incorrect</span></td>");
                                    numErrors++;
                                } else if(modifiedRow["modified_is_tbd"].ToString().Equals("1")) {
                                    modifiedMessage.Append("<td>TBD</td>");
                                    modifiedMessage.Append("<td>" + scrapeOpen + " to " + scrapeClose + "</td>");
                                    modifiedMessage.Append("<td><span style=\"color: #dc3545\">Incorrect</span></td>");
                                    numErrors++;
                                } else if(scrapeOpen != modifiedOpen || scrapeClose != modifiedClose) {
                                    modifiedMessage.Append("<td>" + modifiedOpen + " to " + modifiedClose + "</td>");
                                    modifiedMessage.Append("<td>" + scrapeOpen + " to " + scrapeClose + "</td>");
                                    modifiedMessage.Append("<td><span style=\"color: #dc3545\">Incorrect</span></td>");
                                    numErrors++;
                                } else {
                                    modifiedMessage.Append("<td></td>");
                                    modifiedMessage.Append("<td></td>");
                                    modifiedMessage.Append("<td><span style=\"color: #28a745\">Correct</span></td>");
                                }
                                break;
                        }

                        modifiedMessage.Append("</tr>");
                    }
                }

                return isModified;
            }
        }

        
        // Obsolete
        private static bool dateInRange(DateTime date) {
            DateTime start = getStartDate().Date;

            return DateTime.Compare(start, date.Date) <= 0 && DateTime.Compare(start.AddDays(6), date.Date) >= 0;
        }

        // Obsolete
        private string formatHour(string hour) {
            return Convert.ToDateTime(hour).ToString("h:mm tt");
        }

        private static DateTime getStartDate() {
            DateTime start = DateTime.Now;

            while(start.DayOfWeek != DayOfWeek.Sunday) {
                start = start.AddDays(-1d);
            }

            return start;
        }

        /* The SQL table is manually entered with the names and ids of each venue as it is shown in the Dining hours site.
         * This allows for there to be differences between the venue names of the server and dining hours without problems in the code.
         * 
         * @returns Dictionary<string, int> where key is venue name and value is venue id
         */
        private static Dictionary<string, int> getScraperIds() {
            Dictionary<string, int> venueIds = new Dictionary<string, int>();

            using(OdbcConnection dbConnection = new OdbcConnection(ServerConnection.modoLabsConnStr)) {
                dbConnection.Open();
                OdbcCommand dbCommand = new OdbcCommand();
                dbCommand.Connection = dbConnection;

                dbCommand.CommandText = string.Format(@"SELECT * FROM modo_labs.web_scraper_ids;");
                dbCommand.Parameters.Clear();
                OdbcDataReader dataReader = dbCommand.ExecuteReader();

                while (dataReader.Read()) {
                    venueIds.Add(dataReader.GetString(1), dataReader.GetInt32(2));
                }


                //List<string> test = new List<string>();
                //test.Clear();
                //test.Add("Program did not crash at point 3.");
                //sendEmail(test);
                // Success!


                dataReader.Close();

            }

            return venueIds;
        }

        // Helper method to view data tables
        // Prints to console
        private static void printDataTable(DataTable tbl) {
            string line = "";
            int end = 0;
            int limit = 10;
            foreach(DataColumn item in tbl.Columns) {
                end += 1;
                line += item.ColumnName + "   ";

                if(end == limit) break;
            }
            line += "\n";
            foreach(DataRow row in tbl.Rows) {
                end = 0;
                for(int i = 0; i < tbl.Columns.Count; i++) {
                    line += row[i].ToString() + "   ";
                    end += 1;
                    if(end == limit) break;
                }
                line += "\n";
            }
            Console.WriteLine(line);
        }

        private static DataTable getDiningHours() {
            DataTable dt = new DataTable();

            using(OdbcConnection dbConnection = new OdbcConnection(ServerConnection.modoLabsConnStr)) {
                dbConnection.Open();
                OdbcCommand dbCommand = new OdbcCommand();
                dbCommand.Connection = dbConnection;

                dbCommand.CommandText = string.Format(@"SELECT * FROM modo_labs.dining_hours;");
                dbCommand.Parameters.Clear();
                OdbcDataReader dataReader = dbCommand.ExecuteReader();
                
                dt.Load(dataReader);

                dataReader.Close();

            }

            return dt;

        }

        private static DataTable getModifiedHours() {
            DataTable dt = new DataTable();

            using(OdbcConnection dbConnection = new OdbcConnection(ServerConnection.modoLabsConnStr)) {
                dbConnection.Open();
                OdbcCommand dbCommand = new OdbcCommand();
                dbCommand.Connection = dbConnection;

                dbCommand.CommandText = string.Format(@"SELECT * FROM modo_labs.dining_modified_hours;");
                dbCommand.Parameters.Clear();
                OdbcDataReader dataReader = dbCommand.ExecuteReader();

                dt.Load(dataReader);

                dataReader.Close();

            }

            return dt;
        }

        private static string getSiteSettings(string setting_name) {
            DataTable dt = new DataTable();

            using(OdbcConnection dbConnection = new OdbcConnection(ServerConnection.modoLabsConnStr)) {
                dbConnection.Open();
                OdbcCommand dbCommand = new OdbcCommand();
                dbCommand.Connection = dbConnection;
                
                dbCommand.CommandText = string.Format("SELECT * FROM modo_labs.site_settings WHERE setting_name = ?");
                dbCommand.Parameters.Clear();
                dbCommand.Parameters.AddWithValue(@"setting_name", setting_name);
                OdbcDataReader dataReader = dbCommand.ExecuteReader();

                dt.Load(dataReader);

                dataReader.Close();

            }

            if(dt.Rows.Count == 0) {
                return "";
            } else {
                int value = dt.Columns["value"].Ordinal;
                return dt.Rows[0][value].ToString(); 
            }

        }

        private static void sendEmail(List<string> log) {
            //foreach (string line in log) {
            //    Console.WriteLine(line);
            //}

            //return;
            string tr_styled = "<tr style=\"border:1px solid black; padding: 10px;\">";
            string tr_first_styled = "<tr style=\"border:1px solid black; padding: 10px; background-color: #fcc23f; font-weight: bold;\">";
            string th_styled = "<th style=\"border:1px solid black; padding: 10px; width: 100px;\">";
            string td_styled = "<td style=\"border:1px solid black; padding: 10px;\">";
            string table_styled = "<table style=\"border-collapse: collapse; text-align: center;\">";

            try {

                SmtpClient mySmtpClient = new SmtpClient("127.0.0.1");
                /**mySmtpClient.EnableSsl = true;
                 // set smtp-client with basicAuthentication
                 mySmtpClient.UseDefaultCredentials = false;
                 System.Net.NetworkCredential basicAuthenticationInfo = new
                    System.Net.NetworkCredential("username", "password");
                 mySmtpClient.Credentials = basicAuthenticationInfo;
                **/
                // add from,to mailaddresses
                MailAddress from = new MailAddress("stampweb@umd.edu");
                MailAddress to = new MailAddress("ramor12@umd.edu");
                //MailAddress utsweb = new MailAddress("uts-web@umd.edu");
                MailAddress myco = new MailAddress("mpaulo@umd.edu");
                MailAddress test = new MailAddress("ramor12@umd.edu");

                MailMessage myMail = new System.Net.Mail.MailMessage(from, to);
                //myMail.Bcc.Add(utsweb);
                myMail.Bcc.Add(myco);
                myMail.Bcc.Add(test);

                // add ReplyTo
                /*MailAddress replyTo = new MailAddress("reply@example.com");
                myMail.ReplyToList.Add(replyTo);*/

                // set subject and encoding
                myMail.Subject = "Windows Service: Stamp ModoLabs";
                myMail.SubjectEncoding = System.Text.Encoding.UTF8;

                // set body-message and encoding

                foreach(string logLine in log) {
                    string styled_line = logLine.Replace("<tr*>", tr_first_styled);
                    styled_line = styled_line.Replace("<tr>", tr_styled);
                    styled_line = styled_line.Replace("<th>", th_styled);
                    styled_line = styled_line.Replace("<td>", td_styled);
                    styled_line = styled_line.Replace("<table>", table_styled);
                    myMail.Body += styled_line + "<br>";
                }

                myMail.BodyEncoding = System.Text.Encoding.UTF8;
                // text or html
                myMail.IsBodyHtml = true;

                //Console.WriteLine(myMail.Body);
                mySmtpClient.Send(myMail);
                Console.WriteLine("OK");


            } catch(SmtpException ex) {
                throw new ApplicationException
                  ("SmtpException has occured: " + ex.Message + "-------------------------------" + ex.InnerException);
            } catch(Exception ex) {
                throw ex;
            }

        }

        // console app's entry point
        public static void Main(string[] args)
        {
            string siteSettings = getSiteSettings("dining_hours_scraper");

            List<string> log = new List<string>();
            //log.Add("Site settings: " + siteSettings);
            //log.Add("siteSettings.Equals(\"true\") " + siteSettings.Equals("true").ToString());
            //sendEmail(log);

            if (siteSettings.Equals("true")) {
                CheckDiningHours();
            }

            //Console.WriteLine("Press any key to exit");
            //Console.ReadLine();
        }

    }

}


// Unused Code
//Console.WriteLine("Modified Hours:");

//foreach (DataRow modifiedRow in modified.Rows) {
//    int dining_id = (int)modifiedRow[1];
//    DateTime date = Convert.ToDateTime(modifiedRow[2].ToString());
//    bool valid = scraped.Columns.Contains(date.ToString("%M/d/yyyy"));

//    int pos = -1;
//    if(valid) {
//        pos = scraped.Columns[date.ToString("%M/d/yyyy")].Ordinal;
//    } else {
//        continue;
//    }

//    // Only check the hours that can be checked
//    if(dateInRange(date)) {
//        foreach (DataRow scrapedRow in scraped.Rows) {
//            //Console.WriteLine(venueIds[dining_id] + " == " + scrapedRow[0].ToString() + ": " + (Regex.Replace(scrapedRow[0].ToString(), @"[^A-Za-z0-9]+", "") == Regex.Replace(venueIds[dining_id], @"[^A-Za-z0-9]+", "")).ToString());

//            if(Regex.Replace(scrapedRow[0].ToString(), @"[^A-Za-z0-9]+", "") == Regex.Replace(venueIds[dining_id], @"[^A-Za-z0-9]+", "")) {
//                switch (scrapedRow[pos]) {
//                    case "Closed":
//                        if(scrapedRow[5].ToString() != "0") {
//                            Console.WriteLine("Error in " + date.ToString("%M/d/yyyy"));
//                            Console.WriteLine("Scraped: Closed");
//                            Console.WriteLine("Server: Open");
//                        }
//                        break;
//                    case "TBD":
//                        if(modifiedRow[6].ToString() != "0") {
//                            Console.WriteLine("Error in " + date.ToString("%M/d/yyyy"));
//                            Console.WriteLine("Scraped: TBD");
//                            Console.WriteLine("Server: Not TBD");
//                        }
//                        break;
//                    default:
//                        string[] hours = scrapedRow[pos].ToString().Split('-');
//                        string scrapeOpen = Convert.ToDateTime(hours[0]).ToString("h:mm tt");
//                        string scrapeClose = Convert.ToDateTime(hours[1]).ToString("h:mm tt");

//                        string modifiedOpen = Convert.ToDateTime(modifiedRow[3].ToString()).ToString("h:mm tt");
//                        string modifiedClose = Convert.ToDateTime(modifiedRow[4].ToString()).ToString("h:mm tt");

//                        if(modifiedRow[5].ToString() == "0") {
//                            Console.WriteLine("Error in " + date.ToString("%M/d/yyyy"));
//                            Console.WriteLine("Scraped: " + scrapeOpen + " to " + scrapeClose);
//                            Console.WriteLine("Server: Closed");
//                        } else if(scrapeOpen != modifiedOpen || scrapeClose != modifiedClose) {
//                            Console.WriteLine("Error in " + date.ToString("%M/d/yyyy"));
//                            Console.WriteLine("Scraped: " + scrapeOpen + " to " + scrapeClose);
//                            Console.WriteLine("Server: " + modifiedOpen + " to " + modifiedClose);
//                        }

//                        break;
//                }
//                break;
//            }
//        }

//    }
//}

//for (int weekDay = 0; weekDay < 7; weekDay++) {
//    switch (scrapedRow[pos + weekDay].ToString()) {
//        case "Closed":
//            if(normalRow[20 + weekDay].ToString() != "0") {
//                Console.WriteLine("Error in " + (day.AddDays(weekDay)).ToString("%M/dd/yyyy"));
//                Console.WriteLine("Scraped: Closed");
//                Console.WriteLine("Server: Open");
//            }
//            break;
//        case "TBD":
//            if(normalRow[27 + weekDay].ToString() != "0") {
//                Console.WriteLine("Error in " + (day.AddDays(weekDay)).ToString("%M/d/yyyy"));
//                Console.WriteLine("Scraped: TBD");
//                Console.WriteLine("Server: Not TBD");
//            }
//            break;
//        default:
//            string[] hours = scrapedRow[pos + weekDay].ToString().Split('-');
//            string scrapeOpen = Convert.ToDateTime(hours[0]).ToString("h:mm tt");
//            string scrapeClose = Convert.ToDateTime(hours[1]).ToString("h:mm tt");

//            string normalOpen = Convert.ToDateTime(normalRow[4 + weekDay * 2].ToString()).ToString("h:mm tt");
//            string normalClose = Convert.ToDateTime(normalRow[5 + weekDay * 2].ToString()).ToString("h:mm tt");


//            if(normalRow[20 + weekDay].ToString() == "0") {
//                Console.WriteLine("Error in " + (day.AddDays(weekDay)).ToString("%M/d/yyyy"));
//                Console.WriteLine("Scraped: " + scrapeOpen + " to " + scrapeClose);
//                Console.WriteLine("Server: Closed");
//            } else if(scrapeOpen != normalOpen || scrapeClose != normalClose) {
//                Console.WriteLine("Error in " + (day.AddDays(weekDay)).ToString("%M/d/yyyy"));
//                Console.WriteLine("Scraped: " + scrapeOpen + " to " + scrapeClose);
//                Console.WriteLine("Server: " + normalOpen + " to " + normalClose);
//            }

//            break;
//    }
//} 


//foreach(DataRow normalRow in normal.Rows) {
//    if(Regex.Replace(normalRow[1].ToString(), @"[^A-Za-z0-9]+", "") == Regex.Replace(venueName, @"[^A-Za-z0-9]+", "")) {

//    }
//}
using System.Data;
using System.Net;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using static WebScraper.HelperMethods;

namespace WebScraper {
    class Scraper {
        static void Main(string[] args) {
            //String url = "https://dining.umd.edu/hours-locations/cafes";
            string url = "https://docs.google.com/spreadsheets/d/1XppW_tfjvk_PlKaE4gXF2TtIUiB_iyQguU-VFA4Fh3g/gviz/tq?gid=2021515491";

            JObject html = GetJSON(url);
        }

        private static string GetHTML(string url) {
            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(url);

            return doc.Text;
        }

        private static JObject GetJSON(string url) {
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
            List<String> dates = new List<String>();

            foreach (JToken date in dateJson.Skip(1)) {
                dates.Add(date["v"].ToString());
            }

            foreach(JToken venue in rows.Skip(1)) {
                string venueName = venue.First().First().First()["v"].ToString();
                venues.Add(venueName, new List<String>());

                foreach(JToken date in venue.First().First().Skip(1)) {
                    venues[venueName].Add(date["v"].ToString());
                }

                // Should be all 246
                //Console.WriteLine(venues[venueName].Count());
            }
            // Should also be 246
            //Console.WriteLine(dates.Count());

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

            // Print out datatable
            printDataTable(dt);

            return json;
        }
    }
}
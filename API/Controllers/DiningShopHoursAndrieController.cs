using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umd.Dsa.ModoLabs.Service.Classes;
using Umd.Dsa.ModoLabs.Service.UiComponents;
using Umd.Dsa.ModoLabs.Service.UiComponents.Elements;
using MySqlConnector;
using System.Data;
using Umd.Dsa.ModoLabs.Service.Enums;
using Umd.Dsa.ModoLabs.Service.UiComponents.Elements.Classes;
using System.Reflection.Emit;
using System.Globalization;
using System.Linq;

namespace ModoLabsStudentWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiningShopHoursAndrieController : ModoLabsController
    {
        private readonly IConfiguration _configuration;
        //private bool alternate = true;

        public DiningShopHoursAndrieController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private DataTable GetDiningHours() 
        {
            using MySqlConnection mySqlConnection = new (_configuration.GetConnectionString("Modo"));
            mySqlConnection.Open();
            
            using MySqlCommand mySqlCommand = new MySqlCommand();
            mySqlCommand.Connection = mySqlConnection;

            mySqlCommand.CommandText = string.Format(@"
                SELECT * FROM modo_labs.dining_hours;");

            using MySqlDataReader mySqlDataReader = mySqlCommand.ExecuteReader();
            DataTable dataTable = new DataTable();
            dataTable.Load(mySqlDataReader);
            
            return dataTable;
        }

        private DataTable GetSpecialDiningHours() {
            using MySqlConnection mySqlConnection = new(_configuration.GetConnectionString("Modo"));
            mySqlConnection.Open();

            using MySqlCommand mySqlCommand = new MySqlCommand();
            mySqlCommand.Connection = mySqlConnection;

            mySqlCommand.CommandText = string.Format(@"
                SELECT * FROM modo_labs.special_hours;");

            using MySqlDataReader mySqlDataReader = mySqlCommand.ExecuteReader();
            DataTable dataTable = new DataTable();
            dataTable.Load(mySqlDataReader);

            return dataTable;
        }

        private String formatDiningHours(DateTime openTime, DateTime closeTime) {
            return Convert.ToDateTime(openTime, new CultureInfo("en-us")).ToString("t") + " to " + Convert.ToDateTime(closeTime, new CultureInfo("en-us")).ToString("t");
        }

        private string GetDiningHallImage(int store_id)
        {
            using MySqlConnection mySqlConnection = new(_configuration.GetConnectionString("Modo"));
            mySqlConnection.Open();

            using MySqlCommand mySqlCommand = new MySqlCommand();
            mySqlCommand.Connection = mySqlConnection;

            mySqlCommand.CommandText = string.Format(@"
                SELECT * FROM modo_labs.dining_hall_images WHERE dining_hall_id = @store_id LIMIT 1;");
            mySqlCommand.Parameters.AddWithValue("@store_id", store_id.ToString());
            using MySqlDataReader mySqlDataReader = mySqlCommand.ExecuteReader();
            string url = "https://www.salonlfc.com/wp-content/uploads/2018/01/image-not-found-1-scaled-1150x647.png";
            if (mySqlDataReader.Read()) {
                url = mySqlDataReader["imageURL"].ToString();
            }

            return url;
        }

        private Container GetStatusLists(Dictionary<String, String> dict) 
        {
            StatusList statusList = new StatusList() {
                ListStyle = ListStyle.grouped,
            };

            foreach(KeyValuePair<String, String> kvp in dict) {
                statusList.Items.Add(new StatusListItem() {
                    //Title = title, //dr[1].ToString()
                    Status = Status.available,
                    StatusDescriptor = "<b>" + kvp.Value + "</b>",  //style=\"color:black;\"
                    //StatusDetails = {
                    //    new StatusListItemDetail() {
                    //        Value = kvp.Value
                    //    }
                    //}
                });
            }

            return new Container()
            {
                Content = 
                {
                    statusList  
                }
            };
        }

        private String GetJson()
        {
            DataTable diningHours = GetDiningHours();
            DataTable speiclaDiningHours = GetSpecialDiningHours();

            DayOfWeek today = DateTime.Today.DayOfWeek;
            CardSet cardSet = new CardSet();

            List<Collapsible> collapsibleList = new List<Collapsible>();


            foreach(DataRow dr in diningHours.Rows) {
                string openTime = dr[2 * (int)today + 2].ToString();
                string closeTime = dr[2 * (int)today + 3].ToString();

                string label;
                string labelColor;

                if(TimeSpan.Compare(Convert.ToDateTime(openTime).TimeOfDay, DateTime.Now.TimeOfDay) < 1 &&
                    TimeSpan.Compare(Convert.ToDateTime(openTime).TimeOfDay, DateTime.Now.TimeOfDay) < 1) {
                    // Diner is Currently Open

                    if(TimeSpan.Compare(Convert.ToDateTime(closeTime).TimeOfDay, DateTime.Now.AddHours(1.0).TimeOfDay) < 1) {
                        // Diner closes in 1 hour
                        label = "Closes in 1 hour";
                        labelColor = "#ffc107";
                    } else {
                        // Diner is open for longer than 1 hour
                        label = "Open";
                        labelColor = "#28a745";
                    }
                } else {
                    // Diner is closed
                    label = "Closed";
                    labelColor = "#dc3545";
                }

                ContentCard contentCard = new ContentCard() 
                {
                    Image = 
                    {
                        Url = GetDiningHallImage(int.Parse(dr[0].ToString()))/*imageURLs[dr[1].ToString()]*/
                    },
                    ImageStyle = ImageStyle.heroLarge,
                    ImageHorizontalPosition = HorizontalPosition.right, //alternate ? HorizontalPosition.right : HorizontalPosition.left,
                    Badge = 
                        {
                            Label = label,
                            BackgroundColor = labelColor
                        },
                    Label = "<b>" + label + "</b>",
                    LabelTextColor = labelColor
                };

                //alternate = !alternate;

                contentCard.Title = dr[1].ToString();
                contentCard.TitleTextColor = "#ffffff"; ;

                contentCard.Description = "<br>" + formatDiningHours(Convert.ToDateTime(openTime), Convert.ToDateTime(closeTime));
                contentCard.DescriptionTextColor = "#ffffff";

                contentCard.Size = CardSize.small;
                contentCard.BackgroundColor = "#7D1423";
                contentCard.BorderRadius = BorderRadius.medium;

                cardSet.Items.Add(contentCard);

                /* ************************************* */

                Dictionary<String, String> weekHours = new Dictionary<String, String>()
                {
                    {"Sunday", "Sunday: " + formatDiningHours(Convert.ToDateTime(dr[14].ToString()), Convert.ToDateTime(dr[15].ToString())) },
                    {"Monday", "<br>" + "Monday: " + formatDiningHours(Convert.ToDateTime(dr[2].ToString()), Convert.ToDateTime(dr[3].ToString())) },
                    {"Tuesday", "<br>" + "Tuesday: " + formatDiningHours(Convert.ToDateTime(dr[4].ToString()), Convert.ToDateTime(dr[5].ToString())) },
                    {"Wednesday", "<br>" + "Wednesday: " + formatDiningHours(Convert.ToDateTime(dr[6].ToString()), Convert.ToDateTime(dr[7].ToString())) },
                    {"Thursday", "<br>" + "Thursday: " + formatDiningHours(Convert.ToDateTime(dr[8].ToString()), Convert.ToDateTime(dr[9].ToString())) },
                    {"Friday", "<br>" + "Friday: " + formatDiningHours(Convert.ToDateTime(dr[10].ToString()), Convert.ToDateTime(dr[11].ToString())) },
                    {"Saturday", "<br>" + "Saturday: " + formatDiningHours(Convert.ToDateTime(dr[12].ToString()), Convert.ToDateTime(dr[13].ToString())) }
                };

                //String fullWeekHours = String.Join(" ", new List<String>(weekHours.Values));

                Collapsible collapsible = new Collapsible() 
                {
                    Title = dr[1].ToString(),
                    Badge =
                    {
                        Label = label,
                        BackgroundColor = labelColor
                    },
                    Content =
                    {
                        GetStatusLists(weekHours)
                    }
                };


                collapsibleList.Add(collapsible);
            }

            Container container = new Container() 
            {
                Content = 
                {
                    collapsibleList[0],
                    collapsibleList[1],
                    collapsibleList[2]
                }
            };


            Screen screen = new() 
            {
                ContentContainerWidth = ContentContainerWidth.narrow,
                Content =
                {
                    new BlockHeading() 
                    {
                        Heading = "Today's hours",
                        HeadingLevel = 1
                    },
                    cardSet,
                    new BlockHeading()
                    {
                        Heading = "This week's hours",
                        HeadingLevel = 1
                    },
                    container
                }
            };
            
            return screen.ToString();
        }

        [HttpGet(Name = "DiningShopHoursAndrie")]
        public String Index()
        {
            try
            {
                return GetJson();
            }
            catch (Exception exception)
            {
                return new ErrorScreen(exception.Message).ToString();
            }
        }

    }
}

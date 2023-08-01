using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umd.Dsa.ModoLabs.Service.Classes;
using Umd.Dsa.ModoLabs.Service.UiComponents;
using Umd.Dsa.ModoLabs.Service.UiComponents.Elements;
using MySqlConnector;
using System.Data;
using Umd.Dsa.ModoLabs.Service.Enums;
using Umd.Dsa.ModoLabs.Service.UiComponents.Elements.Classes;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography.Xml;

namespace ModoLabsStudentWebAPI.Controllers {
    [Route("api/[controller]")]
    [ApiController]
    public class DiningShopHoursController : ModoLabsController {
        private readonly IConfiguration _configuration;
        //string darkGreenHex = "#0D5901";
        //string darkRedHex = "#B30006";
        string brightGreenHex = "#28a745";
        string brightRedHex = "#dc3545";
        public DiningShopHoursController(IConfiguration configuration) {
            _configuration = configuration;
        }

        private DataTable GetDiningHours() {
            using MySqlConnection mySqlConnection = new(_configuration.GetConnectionString("Modo"));
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

        //private DataTable GetSpecialDiningHours() {
        //    using MySqlConnection mySqlConnection = new(_configuration.GetConnectionString("Modo"));
        //    mySqlConnection.Open();

        //    using MySqlCommand mySqlCommand = new MySqlCommand();
        //    mySqlCommand.Connection = mySqlConnection;

        //    mySqlCommand.CommandText = string.Format(@"
        //        SELECT * FROM modo_labs.special_hours;");

        //    using MySqlDataReader mySqlDataReader = mySqlCommand.ExecuteReader();
        //    DataTable dataTable = new DataTable();
        //    dataTable.Load(mySqlDataReader);

        //    return dataTable;
        //}

        private DataTable GetSpecialDiningHours(int store_id) {
            using MySqlConnection mySqlConnection = new(_configuration.GetConnectionString("Modo"));
            mySqlConnection.Open();

            using MySqlCommand mySqlCommand = new MySqlCommand();
            mySqlCommand.Connection = mySqlConnection;

            mySqlCommand.CommandText = string.Format(@"
                SELECT * FROM modo_labs.special_hours WHERE store_id = ?;");

            mySqlCommand.Parameters.AddWithValue("store_id", store_id);

            using MySqlDataReader mySqlDataReader = mySqlCommand.ExecuteReader();
            DataTable dataTable = new DataTable();
            dataTable.Load(mySqlDataReader);

            return dataTable;
        }

        private DataTable GetBuildings() {
            using MySqlConnection mySqlConnection = new(_configuration.GetConnectionString("Modo"));
            mySqlConnection.Open();

            using MySqlCommand mySqlCommand = new MySqlCommand();
            mySqlCommand.Connection = mySqlConnection;

            // Gets building by alphabetical order
            mySqlCommand.CommandText = string.Format(@"
                SELECT * FROM modo_labs.buildings ORDER BY name ASC;");

            using MySqlDataReader mySqlDataReader = mySqlCommand.ExecuteReader();
            DataTable dataTable = new DataTable();
            dataTable.Load(mySqlDataReader);

            return dataTable;
        }

        // Currently Unused
        //private string GetDiningHallImage(int store_id) {
        //    using MySqlConnection mySqlConnection = new(_configuration.GetConnectionString("Modo"));
        //    mySqlConnection.Open();

        //    using MySqlCommand mySqlCommand = new MySqlCommand();
        //    mySqlCommand.Connection = mySqlConnection;

        //    mySqlCommand.CommandText = string.Format(@"
        //        SELECT * FROM modo_labs.dining_hall_images WHERE dining_hall_id = @store_id LIMIT 1;");
        //    mySqlCommand.Parameters.AddWithValue("@store_id", store_id.ToString());
        //    using MySqlDataReader mySqlDataReader = mySqlCommand.ExecuteReader();
        //    string url = "https://wallpapercave.com/wp/wp5776683.jpg";
        //    if(mySqlDataReader.Read()) {
        //        url = mySqlDataReader["imageURL"].ToString();
        //    }

        //    return url;
        //}

        private String formatDiningHours(String time) {
            // Ex. 09:00:00 -> 9:00 AM
            return Convert.ToDateTime(time, new CultureInfo("en-us")).ToString("t");
        }

        /* This method is made to check if the special hours are within the same
         * week as the current week so that we know to change the corresponding values.
         */
        private bool datesInSameWeek(DateTime check, DateTime reference)
        {
            DateTime firstDayOfWeek = reference;
            DateTime lastDayOfWeek = firstDayOfWeek.AddDays(6d);

            return check.Date >= firstDayOfWeek.Date && check.Date <= lastDayOfWeek.Date;
        }

        private String GetJsonCollapsible() {
            DataTable diningHours = GetDiningHours();
            //DataTable specialDiningHours = GetSpecialDiningHours();
            DataTable buildings = GetBuildings();

            String[] daysList = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };   
            int todayDayOfWeek = Array.IndexOf(daysList, DateTime.Today.DayOfWeek.ToString());

            Container restaurantsList = new Container();
            Dictionary<int, Collapsible> buildingRestaurants = new Dictionary<int, Collapsible>();
            Dictionary<int, Container> buildingRestaurantsContainers = new Dictionary<int, Container>();

            /*
             * Each building will be a collapsible that will contain a container that holds
             * a nested collapsible. This is a one to many relationship with many restaurants
             * corresponding to one building.
             */
            foreach(DataRow bldg in buildings.Rows) {
                Collapsible bldgCollapsible = new Collapsible();
                bldgCollapsible.Title = bldg["name"].ToString();
                bldgCollapsible.TitleFontWeight = FontWeight.bold;
                bldgCollapsible.TitleTextColor = "#0e1111";
                buildingRestaurants.Add(int.Parse(bldg["id"].ToString()), bldgCollapsible);

                Container bldgContainer = new Container();
                buildingRestaurantsContainers.Add(int.Parse(bldg["id"].ToString()), bldgContainer);
            }

            /*
             * Each restaurant will be mapped to their corresponding building and be added to a list
             * of restaurants as a collapsible.
            */
            foreach(DataRow dr in diningHours.Rows) {
                string todayOpenTime = dr[2 * todayDayOfWeek + 2].ToString();
                string todayCloseTime = dr[2 * todayDayOfWeek + 3].ToString();
                bool isOpenToday = (int.Parse(dr[todayDayOfWeek + 16].ToString()) == 1);
                bool closedForToday = DateTime.Now.TimeOfDay > TimeSpan.Parse(todayCloseTime);
                bool notOpenedForTodayYet = DateTime.Now.TimeOfDay < TimeSpan.Parse(todayOpenTime);
                bool isOpenNow = isOpenToday && !(closedForToday || notOpenedForTodayYet);

                DataTable specialDiningHours = GetSpecialDiningHours(int.Parse(dr[0].ToString()));
                    
                // Tuple of the data row and the day of week
                List<Tuple<DataRow, DayOfWeek>> relevantDates = new List<Tuple<DataRow, DayOfWeek>>();
                List<DataRow> specialDates = new List<DataRow>();
                /* Process Special Dining Hours */
                foreach(DataRow dr2 in specialDiningHours.Rows) {
                    if(DateTime.Compare(Convert.ToDateTime(dr2[3]).Date, DateTime.Now.Date) >= 0) {
                        specialDates.Add(dr2);
                    }
                    // Adds the data row if the dates are within the same week of the corresponding dining hall
                    if(datesInSameWeek(Convert.ToDateTime(dr2[3].ToString()), DateTime.Now)) {
                        relevantDates.Add(Tuple.Create(dr2, Convert.ToDateTime(dr2[3]).DayOfWeek));  
                    }
                }

                Collapsible restaurant = new Collapsible();
                restaurant.Title = "&nbsp;&nbsp;&nbsp;" + dr["store_name"].ToString().ToUpper();
                restaurant.TitleFontWeight = FontWeight.semibold;
                restaurant.TitleTextColor = "#73595c";
                restaurant.WrapperBackgroundColor = "#e8e6e6";
                
                restaurant.Badge.BackgroundColor = isOpenNow ? brightGreenHex : brightRedHex;
                restaurant.Badge.Label = isOpenNow ? "Open" : "Closed";

                EventList dayList = new EventList();

                for(int i = 0; i < 7; i++) {
                    // Checks to see if there is a matching special date (This could possibly be more optimized)
                    bool special = false;
                    DataRow specialRow = null;

                    int day_num;
                    if(i + todayDayOfWeek > 6) {
                        day_num = i + todayDayOfWeek - 7;
                    } else {
                        day_num = i + todayDayOfWeek;
                    }

                    // Behavior may be undefined if there are there are two special dates of the same day in a single restaurant/dining hall
                    foreach(Tuple<DataRow, DayOfWeek> specialDate in relevantDates) {
                        if((int)specialDate.Item2 == day_num) {
                            special = true;
                            specialRow = specialDate.Item1;
                        }
                    }

                    string openTime = dr[2 * day_num + 2].ToString();
                    string closeTime = dr[2 * day_num + 3].ToString();
                    bool isOpenOnDay = (int.Parse(dr[day_num + 16].ToString()) == 1);

                    string description = isOpenOnDay ? formatDiningHours(openTime) + " to " + formatDiningHours(closeTime) : "Closed";

                    string specialOpenTime;
                    string specialCloseTime;
                    // If the day is special, the special date takes priority over normal hours
                    if(special && specialRow[7].ToString() == "1") {
                        specialOpenTime = specialRow[4].ToString();
                        specialCloseTime = specialRow[5].ToString();
                        isOpenOnDay = (int.Parse(specialRow[6].ToString()) == 1);

                        // Special hours can change the badge
                        if((int)DateTime.Now.DayOfWeek == day_num) {
                                if(isOpenOnDay && TimeSpan.Compare(DateTime.Now.TimeOfDay, TimeSpan.Parse(specialOpenTime)) >= 0 &&
                                TimeSpan.Compare(TimeSpan.Parse(specialCloseTime), DateTime.Now.TimeOfDay) >= 0) {
                                restaurant.Badge.Label = "Open";
                                restaurant.Badge.BackgroundColor = brightGreenHex;
                            } else {
                                restaurant.Badge.Label = "Closed";
                                restaurant.Badge.BackgroundColor = brightRedHex;
                            }
                        }
                        description = "<s>" + description + "</s>&nbsp;|&nbsp;" + (isOpenOnDay ? formatDiningHours(specialOpenTime) + " to " + formatDiningHours(specialCloseTime) : "Closed");
                    }

                    EventListItem day = new EventListItem();
                    day.TitleTextColor = "#0e1111";
                    day.DescriptionTextColor = "#73595c";
                    day.DividerColor = "#73595c";
                    day.Title = daysList[day_num].ToString();

                    // Makes the font weight bolder on the current day
                    // Feel free to remove if you don't like
                    if(i == 0) {
                        day.TitleFontWeight = FontWeight.semibold;
                    }

                    day.DatetimeFontSize = "large";
                    DateTime dayOfWeek = DateTime.Now;

                    // Continue subtracting until datetime reaches Sunday
                    while(dayOfWeek.DayOfWeek != DayOfWeek.Sunday) {
                        dayOfWeek = dayOfWeek.AddDays(-1d);
                    }

                    day.DatetimeTextColor = "#0e1111";
                    day.DatetimePrimaryLine = dayOfWeek.AddDays((double)(i + todayDayOfWeek)).ToString("MMMM");
                    day.DatetimeSecondaryLine = dayOfWeek.AddDays((double)(i + todayDayOfWeek)).Day.ToString();

                    day.Description = description;
                    day.DividerColor = isOpenOnDay ? brightGreenHex : brightRedHex;
                    dayList.Items.Add(day);
                }

                string detailDescription = "";

                foreach(DataRow specialRow in specialDates) {
                    if(specialRow[7].ToString() == "1") {
                        string specialOpenTime = specialRow[4].ToString();
                        string specialCloseTime = specialRow[5].ToString();
                        string specialDate = Convert.ToDateTime(specialRow[3].ToString(), new CultureInfo("en-us")).ToString("MMMM d, yyyy");
                        bool isOpenOnDay = (int.Parse(specialRow[6].ToString()) == 1);
                        detailDescription += specialDate + " - " + (isOpenOnDay ? formatDiningHours(specialOpenTime) + " to " + formatDiningHours(specialCloseTime) : "Closed") + "<br>";
                    }
                }

                Detail specialHours = new Detail() {
                    Title = "Adjusted Hours",
                    TitleTextColor = "#0e1111",
                    Description = detailDescription,
                    DescriptionFontSize = "xsmall"
                };

                // The opacity of the background images is set to .8
                // To make it darker, raise the value, and vice versa
                // dayList.BackgroundColor = "rgba(0,0,0,0.8)";
                dayList.ShowTopBorder = false;
                dayList.ShowBottomBorder = false;
                restaurant.Content.Add(dayList);
                if(detailDescription != "") {
                    restaurant.Content.Add(specialHours);
                }
                Container bldg = buildingRestaurantsContainers.GetValueOrDefault(int.Parse(dr["building_id"].ToString()));
                bldg.Content.Add(restaurant);

            }

            // For each building collapsible, it adds a container holding a list of collapsibles for the restaurants
            foreach(int bldgKey in buildingRestaurants.Keys) {
                Collapsible bldgCollapsible = buildingRestaurants.GetValueOrDefault(bldgKey);
                Container bldgContainer = buildingRestaurantsContainers.GetValueOrDefault(bldgKey);
                bldgCollapsible.Content.Add(bldgContainer);
                restaurantsList.Content.Add(bldgCollapsible);
            }


            Screen screen = new() {
                ContentContainerWidth = ContentContainerWidth.narrow,
                Content =
                {
                    new BlockHeading()
                    {
                        Heading = "Dining Hours",
                        HeadingLevel = 1
                    },
                    restaurantsList
                }
            };

            return screen.ToString();
        }


        [HttpGet(Name = "DiningShopHours")]
        public String Index() {
            try {
                //return GetJsonCardSet();
                return GetJsonCollapsible();
            } catch(Exception exception) {
                return new ErrorScreen(exception.Message).ToString();
            }
        }

    }
}


// Currently not used
//private String GetJsonCardSet() {
//    DataTable diningHours = GetDiningHours();
//    DataTable speiclaDiningHours = GetSpecialDiningHours();
//    String[] daysList = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
//    int todayDayOfWeek = Array.IndexOf(daysList, DateTime.Today.DayOfWeek.ToString());
//    CardSet restaurantsList = new CardSet();
//    restaurantsList.Size = Size.large;
//    restaurantsList.MarginTop = "xtight";
//    foreach(DataRow dr in diningHours.Rows) {
//        string imgURL = GetDiningHallImage(int.Parse(dr["store_id"].ToString()));
//        string todayOpenTime = dr[2 * todayDayOfWeek + 2].ToString();
//        string todayCloseTime = dr[2 * todayDayOfWeek + 3].ToString();
//        string tomorrowOpenTime = dr[2 * (todayDayOfWeek + 1) + 2].ToString();
//        bool isOpenToday = (int.Parse(dr[todayDayOfWeek + 16].ToString()) == 1);
//        bool closedForToday = DateTime.Now.TimeOfDay > TimeSpan.Parse(todayCloseTime);
//        bool notOpenedForTodayYet = DateTime.Now.TimeOfDay < TimeSpan.Parse(todayOpenTime);
//        bool isOpenNow = isOpenToday && !((closedForToday) || (notOpenedForTodayYet));

//        CarouselCard restaurant = new CarouselCard();
//        restaurant.Size = Size.normal;
//        restaurant.ImageStyle = ImageStyle.fullbleedSolid;
//        restaurant.Borderless = true;
//        restaurant.BorderRadius = BorderRadius.xloose;
//        CarouselCardItem restaurantFirstCard = new CarouselCardItem();
//        restaurantFirstCard.Title = dr["store_name"].ToString().ToUpper();
//        restaurantFirstCard.TitleTextColor = "#FFFFFF";
//        restaurantFirstCard.TitleFontSize = "2rem";
//        restaurantFirstCard.Description = isOpenNow ? "OPEN NOW" : "CLOSED NOW";
//        restaurantFirstCard.DescriptionFontSize = "1.5rem";
//        restaurantFirstCard.DescriptionTextColor = isOpenNow ? brightGreenHex : brightRedHex;
//        if(isOpenNow) {
//            restaurantFirstCard.Description += " <br> <small>Closes at " + todayCloseTime + "</small>";
//        } else {
//            if(closedForToday) {
//                restaurantFirstCard.Description += " <br> <small>Open tomorrow at " + tomorrowOpenTime + "</small>";
//            } else if(notOpenedForTodayYet) {
//                restaurantFirstCard.Description += " <br> <small>Open today at " + todayOpenTime + "</small>";
//            }
//        }
//        restaurantFirstCard.Image.Url = imgURL;
//        restaurantFirstCard.Image.Alt = "A picture of a dining hall";
//        restaurant.Items.Add(restaurantFirstCard);

//        for(int i = 0; i < 7; i++) {
//            string openTime = dr[2 * i + 2].ToString();
//            string closeTime = dr[2 * i + 3].ToString();
//            bool isOpenOnDay = (int.Parse(dr[i + 16].ToString()) == 1);

//            string green = "https://garden.spoonflower.com/c/12751641/p/f/m/qVPeHXLtQRjpdaA_gX2jf4YgSq6jP7RN7qnsgT67kzX5SWlHJ7gI/Solid%20dark%20spring%20green.jpg";
//            string red = "https://garden.spoonflower.com/c/12359141/p/f/m/ha1oX84VEKgB35UKbIIfHwDk60tUhU_KkGhK-Bof0PUkpVegF9yJ/Solid%20scarlet%20red.jpg";
//            CarouselCardItem restaurantCard = new CarouselCardItem();
//            restaurantCard.Title = daysList[i].ToString().ToUpper();
//            restaurantCard.Image.Url = isOpenOnDay ? green : red;
//            //restaurantCard.Image.Url = imgURL;
//            restaurantCard.TitleTextColor = "white";
//            restaurantCard.TitleFontSize = "2rem";
//            restaurantCard.DescriptionFontSize = "1.5rem";
//            restaurantCard.DescriptionTextColor = isOpenOnDay ? brightGreenHex : brightRedHex;
//            restaurantCard.Description = isOpenOnDay ? "OPEN<br>" : "CLOSED<br>";
//            restaurantCard.Description += openTime + " to " + closeTime;
//            restaurant.Items.Add(restaurantCard);
//        }

//        restaurantsList.Items.Add(restaurant);
//    }


//    Screen screen = new() {
//        ContentContainerWidth = ContentContainerWidth.narrow,
//        Content =
//        {
//            new BlockHeading()
//            {
//                Heading = "Dining Hours",
//                HeadingLevel = 1
//            },
//            restaurantsList
//        }
//    };

//    return screen.ToString();
//}

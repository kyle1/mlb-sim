using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Windows.Forms;
using System.Net;
using System.IO;
using Weighted_Randomizer;
using System.Reflection;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Threading;
using System.Drawing;
using ForecastIO;
using ForecastIO.Extensions;
using System.Drawing.Drawing2D;
using IronWebScraper;

namespace mlb_model
{

    public partial class MLB : Form
    {
        bool initialLoadup = true;
        //string mConnectionString = "Data Source=Kyle-PC\\SQLEXPRESS;Initial Catalog=baseball;Integrated Security=True";
        string mConnectionString = System.IO.File.ReadAllText("../../connection.txt").Replace("\\\\", "\\");
        string key = "214830334e3a4721ad0425603561ecbe"; // Dark Sky API key
        int awayTeamID;
        int homeTeamID;
        int venueID;
        int awayPitcherID;
        int homePitcherID;
        string awayPitcherNote;
        string homePitcherNote;
        List<int> awayBatterIDs = new List<int>();
        List<int> homeBatterIDs = new List<int>();
        DataSet schedule;
        DataRow matchup;
        DataRow venue;
        DataSet awayPitcherStats;
        DataSet homePitcherStats;
        DataSet awayBatterStats;
        DataSet homeBatterStats;

        [JsonProperty("event")]
        public string Event { get; set; }

        public MLB()
        {
            InitializeComponent();
            cboDate.Items.Add(DateTime.Today.AddDays(-1));
            cboDate.Items.Add(DateTime.Today);
            cboDate.Items.Add(DateTime.Today.AddDays(1));
            cboDate.SelectedIndex = 1;
            //InsertNewGameData();
        }

        #region "Events"

        private void CboDate_SelectedIndexChanged(object sender, EventArgs e)
        {
            //foreach (Control item in pnlMatchups.Controls)
            //{
            //    pnlMatchups.Controls.Remove(item);
            //}
            pnlMatchups.Controls.Clear();
            LoadMatchups();
        }

        private void ChkAwayBatterPlatoon_CheckedChanged(object sender, EventArgs e)
        {
            string error = "";
            error += LoadAwayBatters(chkAwayBatterPlatoon.Checked);

        }

        private void ChkHomeBatterPlatoon_CheckedChanged(object sender, EventArgs e)
        {
            LoadHomeBatters(chkHomeBatterPlatoon.Checked);
        }

        private void RadPercents_CheckedChanged(object sender, EventArgs e)
        {
            string error = "";
            error += LoadAwayPitcher();
            error += LoadHomePitcher();
            error += LoadAwayBatters(chkAwayBatterPlatoon.Checked);
            error += LoadHomeBatters(chkHomeBatterPlatoon.Checked);
        }

        private void RadFirstInning_CheckedChanged(object sender, EventArgs e)
        {
            if (radFirstInning.Checked)
            {
                radFirst5Innings.Checked = false;
                radEntireGame.Checked = false;
            }
        }

        private void RadFirst5Innings_CheckedChanged(object sender, EventArgs e)
        {
            if (radFirst5Innings.Checked)
            {
                radFirstInning.Checked = false;
                radEntireGame.Checked = false;
            }
        }

        private void RadEntireGame_CheckedChanged(object sender, EventArgs e)
        {
            if (radEntireGame.Checked)
            {
                radFirstInning.Checked = false;
                radFirst5Innings.Checked = false;
            }
        }

        private void DgvAwayBatters_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            dgvAwayBatters.ClearSelection();
        }

        private void DgvHomeBatters_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            dgvHomeBatters.ClearSelection();
        }

        private void DgvAwayPitcher_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            dgvAwayPitcher.ClearSelection();
        }

        private void DgvHomePitcher_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            dgvHomePitcher.ClearSelection();
        }

        private void DgvFirst5Innings_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            dgvFirst5Innings.ClearSelection();
        }

        private void DgvEntireGame_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            dgvEntireGame.ClearSelection();
        }

        private void BtnSimulate_Click(object sender, EventArgs e)
        {
            Simulate();
        }

        #endregion

        #region "Methods"

        private void InsertNewGameData()
        {
            var mostRecentGameDateInDb = DataAdapter.runSPReturnDS(mConnectionString, "pr_GetMostRecentGameDate", null);
            mostRecentGameDateInDb.Tables[0].TableName = "Game";
            var mostRecentGameDate = Convert.ToDateTime(mostRecentGameDateInDb.Tables[0].Rows[0]["GameDate"]);
            //var beginDateForRequest = mostRecentGameDate.AddDays(1);
            var beginDate = mostRecentGameDate.AddDays(1).ToString("MM/dd/yyyy");
            var endDate = DateTime.Today.AddDays(-1).ToString("MM/dd/yyyy");

            if (Convert.ToDateTime(beginDate) >= Convert.ToDateTime(endDate))
            {
                return;
            }

            // Get missing game data
            string html = string.Empty;
            var url = "https://statsapi.mlb.com/api/v1/schedule?startDate=" + beginDate + "&endDate=" + endDate + "&sportId=1";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.AutomaticDecompression = DecompressionMethods.GZip;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream)) { html = reader.ReadToEnd(); }
            dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(html);

            string error = "";

            List<int> gameIds = new List<int>();

            foreach (var date in json["dates"])
            {
                foreach (var game in date.games)
                {
                    var gameDatetime = Convert.ToDateTime(game.gameDate).ToLocalTime();

                    if (game["status"].codedGameState == "F" && gameDatetime > mostRecentGameDate)
                    {
                        DataRow newGame = mostRecentGameDateInDb.Tables["Game"].NewRow();
                        newGame["GameID"] = game.gamePk;
                        newGame["GameDate"] = (Convert.ToDateTime(game.gameDate)).ToLocalTime(); //todo convert to pst
                        newGame["Status"] = game["status"].detailedState;
                        newGame["AwayTeamID"] = game["teams"]["away"]["team"].id;
                        newGame["AwayTeamScore"] = game["teams"]["away"].score;
                        newGame["AwayTeamRecordWins"] = game["teams"]["away"]["leagueRecord"].wins;
                        newGame["AwayTeamRecordLosses"] = game["teams"]["away"]["leagueRecord"].losses;
                        newGame["AwayTeamRecordPct"] = game["teams"]["away"]["leagueRecord"].pct;
                        newGame["HomeTeamID"] = game["teams"]["home"]["team"].id;
                        newGame["HomeTeamScore"] = game["teams"]["home"].score;
                        newGame["HomeTeamRecordWins"] = game["teams"]["home"]["leagueRecord"].wins;
                        newGame["HomeTeamRecordLosses"] = game["teams"]["home"]["leagueRecord"].losses;
                        newGame["HomeTeamRecordPct"] = game["teams"]["home"]["leagueRecord"].pct;
                        newGame["VenueID"] = game["venue"].id;
                        newGame["Season"] = game.seasonDisplay;
                        newGame["DayNight"] = game.dayNight;
                        newGame["GamesInSeries"] = game.gamesInSeries;
                        newGame["SeriesGameNumber"] = game.seriesGameNumber;
                        newGame["SeriesDescription"] = game.seriesDescription;
                        mostRecentGameDateInDb.Tables["Game"].Rows.Add(newGame);

                        gameIds.Add((int)game.gamePk);
                    }
                }
            }
            DataAdapter.UpdateDb(mConnectionString, "SELECT * FROM Game WHERE 1 = 2", mostRecentGameDateInDb, "Game");

            var counter = 1;
            foreach (var gameId in gameIds)
            {
                DataSet pbp = DataAdapter.runSPReturnDS(mConnectionString, "pr_GetPlayByPlayColumns", null);
                pbp.Tables[0].TableName = "PlayByPlay";

                // Get missing pbp data
                string pbpHtml = string.Empty;
                var pbpUrl = "https://statsapi.mlb.com/api/v1/game/" + gameId.ToString() + "/playByPlay";
                HttpWebRequest pbpRequest = (HttpWebRequest)WebRequest.Create(pbpUrl);
                pbpRequest.AutomaticDecompression = DecompressionMethods.GZip;
                using (HttpWebResponse response = (HttpWebResponse)pbpRequest.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream)) { pbpHtml = reader.ReadToEnd(); }
                dynamic pbpJson = Newtonsoft.Json.JsonConvert.DeserializeObject(pbpHtml);

                foreach (var play in pbpJson["allPlays"])
                {
                    DataRow pbpDataRow = pbp.Tables[0].NewRow();

                    pbpDataRow["PlayByPlayID"] = counter;
                    pbpDataRow["GameID"] = gameId;
                    pbpDataRow["BatterID"] = play["matchup"]["batter"].id;
                    pbpDataRow["BatSide"] = play["matchup"]["batSide"].code;
                    pbpDataRow["BatterSplit"] = play["matchup"]["splits"].batter;
                    pbpDataRow["PitcherID"] = play["matchup"]["pitcher"].id;
                    pbpDataRow["PitchHand"] = play["matchup"]["pitchHand"].code;
                    pbpDataRow["PitcherSplit"] = play["matchup"]["splits"].pitcher;
                    pbpDataRow["MenOnBase"] = play["matchup"]["splits"].menOnBase;
                    pbpDataRow["EventType"] = play["result"].eventType;
                    pbpDataRow["IsScoringPlay"] = play["about"].isScoringPlay;
                    pbpDataRow["AwayTeamScore"] = play["result"].awayScore;
                    pbpDataRow["HomeTeamScore"] = play["result"].homeScore;
                    pbpDataRow["AtBatIndex"] = play["about"].atBatIndex;
                    pbpDataRow["HalfInning"] = play["about"].halfInning;
                    pbpDataRow["Inning"] = play["about"].inning;
                    pbpDataRow["Outs"] = play["count"].outs;

                    foreach (var ev in play["result"])
                    {
                        if (ev.Name == "event")
                        {
                            pbpDataRow["Event"] = ev.First; //had to assign value this way sent the property is a keyword
                        }
                    }

                    pbp.Tables[0].Rows.Add(pbpDataRow);
                }

                DataAdapter.UpdateDb(mConnectionString, "SELECT * FROM PlayByPlay WHERE 1 = 2", pbp, "PlayByPlay");
                Thread.Sleep(3000); //sleep 3 seconds
            }
        }

        private void LoadMatchups()
        {
            SqlParameterCollection sqlParams = new SqlCommand().Parameters;
            sqlParams.Clear();
            sqlParams.AddWithValue("@GameDate", cboDate.SelectedItem);
            schedule = DataAdapter.runSPReturnDS(mConnectionString, "pr_GetGamesByDate", sqlParams);

            var buttonCounter = 0;
            var firstGroupBox = new GroupBox();
            foreach (DataRow game in schedule.Tables[0].Rows)
            {
                var groupBox = new GroupBox();
                groupBox.Location = new Point(12 + (100 * buttonCounter), 5);
                groupBox.Size = new Size(95, 75);
                groupBox.Name = Convert.ToString(game["MlbGameID"]);
                groupBox.Font = new Font("Segoe UI", 9);
                //groupBox.Click += btnMatchup_Click;
                groupBox.Click += grpMatchup_Click;
                pnlMatchups.Controls.Add(groupBox);

                var timeLabel = new Label();
                timeLabel.Location = new Point(5, 10);
                timeLabel.Size = new Size(70, 15);
                timeLabel.Text = Convert.ToString(game["GameTimePST"]);
                timeLabel.Click += grpMatchup_Click;
                groupBox.Controls.Add(timeLabel);

                var awayLogo = new PictureBox();
                awayLogo.Name = Convert.ToString(game["MlbGameID"]);
                awayLogo.Location = new Point(5, 30);
                awayLogo.Size = new Size(20, 20);
                awayLogo.SizeMode = PictureBoxSizeMode.Zoom;
                awayLogo.Click += grpMatchup_Click;
                Image awayImg = Image.FromFile("C:\\Users\\Kyle\\Desktop\\mlb_model\\mlb_model\\img\\team_logos\\" + Convert.ToString(game["AwayTeamAbbrev"]) + ".png");

                awayLogo.Image = awayImg;

                groupBox.Controls.Add(awayLogo);

                var awayLabel = new Label();
                awayLabel.Name = Convert.ToString(game["MlbGameID"]);
                awayLabel.Location = new Point(25, 32);
                awayLabel.Size = new Size(35, 15);
                awayLabel.Text = Convert.ToString(game["AwayTeamAbbrev"]);
                awayLabel.Click += grpMatchup_Click;
                groupBox.Controls.Add(awayLabel);

                var homeLogo = new PictureBox();
                homeLogo.Name = Convert.ToString(game["MlbGameID"]);
                homeLogo.Location = new Point(5, 50);
                homeLogo.Size = new Size(20, 20);
                homeLogo.SizeMode = PictureBoxSizeMode.Zoom;
                homeLogo.Click += grpMatchup_Click;
                Image homeImg = Image.FromFile("C:\\Desktop\\mlb_model\\mlb_model\\img\\team_logos\\" + Convert.ToString(game["HomeTeamAbbrev"]) + ".png");
                homeLogo.Image = homeImg;
                groupBox.Controls.Add(homeLogo);

                var homeLabel = new Label();
                homeLabel.Name = Convert.ToString(game["MlbGameID"]);
                homeLabel.Location = new Point(25, 52);
                homeLabel.Text = Convert.ToString(game["HomeTeamAbbrev"]);
                homeLabel.Size = new Size(35, 15);
                homeLabel.Click += grpMatchup_Click;
                groupBox.Controls.Add(homeLabel);

                if (buttonCounter == 0)
                {
                    firstGroupBox = groupBox;
                }

                buttonCounter += 1;
            }

            grpMatchup_Click(firstGroupBox, null);
        }

        private void GetVenueData()
        {
            SqlParameterCollection sqlParams = new SqlCommand().Parameters;
            sqlParams.Clear();
            sqlParams.AddWithValue("@VenueID", venueID);
            venue = DataAdapter.runSPReturnDS(mConnectionString, "pr_GetVenueDataByVenueID", sqlParams).Tables[0].Rows[0];
            lblVenue.Text = venue["Name"].ToString();
            lblParkFactor.Text = "Park Factor: " + Math.Round(Convert.ToDecimal(venue["ParkFactor"]), 2).ToString();
        }

        private void GetWeatherData()
        {
            DateTime gameTime = Convert.ToDateTime(matchup["GameDateTimePST"]);
            double latitude = Convert.ToDouble(venue["Latitude"]);
            double longitude = Convert.ToDouble(venue["Longitude"]);

            var hour = gameTime.Hour;
            var request = new ForecastIORequest(key, (float)latitude, (float)longitude, gameTime, Unit.us);
            var response = request.Get();
            var windSpeed = Math.Round(response.hourly.data[hour].windSpeed);
            var windBearing = (int)response.hourly.data[hour].windBearing;
            var temperature = Math.Round(response.hourly.data[hour].temperature);
            var adjustedWindBearing = GetAdjustedWindBearing(windBearing, Convert.ToInt32(venue["BatterBearing"]));
            lblTemp.Text = temperature.ToString() + "\u00B0";
            Image arrow = Image.FromFile("C:\\Desktop\\mlb_model\\mlb_model\\img\\arrow.jpg");
            arrow = RotateImage(arrow, adjustedWindBearing);
            picWindDirection.Image = arrow;
            picWindDirection.Size = new Size(30, 30);

            lblWindSpeed.Text = windSpeed.ToString() + " mph";
        }

        public static Image RotateImage(Image img, float rotationAngle)
        {
            //create an empty Bitmap image
            Bitmap bmp = new Bitmap(img.Width, img.Height);

            //turn the Bitmap into a Graphics object
            Graphics gfx = Graphics.FromImage(bmp);

            //now we set the rotation point to the center of our image
            gfx.TranslateTransform((float)bmp.Width / 2, (float)bmp.Height / 2);

            //now rotate the image
            gfx.RotateTransform(rotationAngle);

            gfx.TranslateTransform(-(float)bmp.Width / 2, -(float)bmp.Height / 2);

            //set the InterpolationMode to HighQualityBicubic so to ensure a high
            //quality image once it is transformed to the specified size
            gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;

            //now draw our new image onto the graphics object
            gfx.DrawImage(img, new Point(0, 0));

            //dispose of our Graphics object
            gfx.Dispose();

            //return the image
            return bmp;
        }

        public void CheckDataSet(DataSet dataSet)
        {
            Assembly assembly = Assembly.LoadFrom(@"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0\System.Data.dll");
            Type type = assembly.GetType("System.Data.ConstraintEnumerator");
            ConstructorInfo ctor = type.GetConstructor(new[] { typeof(DataSet) });
            object instance = ctor.Invoke(new object[] { dataSet });
            BindingFlags bf = BindingFlags.Instance | BindingFlags.Public;
            MethodInfo m_GetNext = type.GetMethod("GetNext", bf);

            while ((bool)m_GetNext.Invoke(instance, null))
            {
                bool flag = false;
                MethodInfo m_GetConstraint = type.GetMethod("GetConstraint", bf);
                Constraint constraint = (Constraint)m_GetConstraint.Invoke(instance, null);
                Type constraintType = constraint.GetType();
                BindingFlags bfInternal = BindingFlags.Instance | BindingFlags.NonPublic;
                MethodInfo m_IsConstraintViolated = constraintType.GetMethod("IsConstraintViolated", bfInternal);
                flag = (bool)m_IsConstraintViolated.Invoke(constraint, null);
                if (flag)
                    Debug.WriteLine("Constraint violated, ConstraintName: " + constraint.ConstraintName + ", tableName: " + constraint.Table);
            }

            foreach (DataTable table in dataSet.Tables)
            {
                foreach (DataColumn column in table.Columns)
                {
                    Type columnType = column.GetType();
                    BindingFlags bfInternal = BindingFlags.Instance | BindingFlags.NonPublic;

                    bool flag = false;
                    if (!column.AllowDBNull)
                    {
                        MethodInfo m_IsNotAllowDBNullViolated = columnType.GetMethod("IsNotAllowDBNullViolated", bfInternal);
                        flag = (bool)m_IsNotAllowDBNullViolated.Invoke(column, null);
                        if (flag)
                        {
                            Debug.WriteLine("DBnull violated  --> ColumnName: " + column.ColumnName + ", tableName: " + column.Table.TableName);
                        }
                    }
                    if (column.MaxLength >= 0)
                    {
                        MethodInfo m_IsMaxLengthViolated = columnType.GetMethod("IsMaxLengthViolated", bfInternal);
                        flag = (bool)m_IsMaxLengthViolated.Invoke(column, null);
                        if (flag)
                            Debug.WriteLine("MaxLength violated --> ColumnName: " + column.ColumnName + ", tableName: " + column.Table.TableName);
                    }
                }
            }
        }

        private string GetProbablePitchers(int gameId)
        {
            // Get probable pitchers
            string html = string.Empty;
            //var selectedGameID = cboMatchups.SelectedValue;
            //var url = "https://statsapi.mlb.com/api/v1/schedule?gamePk=" + selectedGameID + "&hydrate=probablePitcher(note)&fields=dates,date,games,gamePk,gameDate,status,abstractGameState,teams,away,home,team,id,name,probablePitcher,id,fullName,note";
            var url = "https://statsapi.mlb.com/api/v1/schedule?gamePk=" + gameId + "&hydrate=probablePitcher(note)&fields=dates,date,games,gamePk,gameDate,status,abstractGameState,teams,away,home,team,id,name,probablePitcher,id,fullName,note";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.AutomaticDecompression = DecompressionMethods.GZip;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream)) { html = reader.ReadToEnd(); }
            dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(html);

            string error = "";

            //Note: In the event that a game is postponed, the first index of "dates" in the json object will be
            //the original game date. For this reason, take the last date in "dates" to get the correct data.
            if (json["dates"][json["dates"].Count - 1].games[0].teams["away"]["probablePitcher"] != null)
            {
                awayPitcherID = json["dates"][json["dates"].Count - 1].games[0].teams["away"]["probablePitcher"].id;
                awayPitcherNote = json["dates"][json["dates"].Count - 1].games[0].teams["away"]["probablePitcher"].note;
                lblAwayPitcherNote.Text = awayPitcherNote;
            }
            else
            {
                error += "Away team pitcher not set." + Environment.NewLine;
            }

            if (json["dates"][json["dates"].Count - 1].games[0].teams["home"]["probablePitcher"] != null)
            {
                homePitcherID = json["dates"][json["dates"].Count - 1].games[0].teams["home"]["probablePitcher"].id;
                homePitcherNote = json["dates"][json["dates"].Count - 1].games[0].teams["home"]["probablePitcher"].note;
                lblHomePitcherNote.Text = homePitcherNote;
            }
            else
            {
                error += "Home team pitcher not set." + Environment.NewLine;
            }

            return error;
        }

        private string GetLineups(int gameId)
        {
            //Get lineups
            string html = string.Empty;
            //var selectedGameID = cboMatchups.SelectedValue;
            //var url = "https://statsapi.mlb.com/api/v1/schedule?gamePk=" + selectedGameID + "&hydrate=lineups";
            var url = "https://statsapi.mlb.com/api/v1/schedule?gamePk=" + gameId + "&hydrate=lineups";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.AutomaticDecompression = DecompressionMethods.GZip;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream)) { html = reader.ReadToEnd(); }
            dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(html);

            string error = "";

            awayBatterIDs.Clear();
            //Note: In the event that a game is postponed, the first index of "dates" in the json object will be
            //the original game date. For this reason, take the last date in "dates" to get the correct data.
            if (json["dates"][json["dates"].Count - 1].games[0]["lineups"]["awayPlayers"] != null)
            {
                foreach (var player in json["dates"][json["dates"].Count - 1].games[0]["lineups"]["awayPlayers"])
                {
                    int awayPlayerID = player.id;
                    awayBatterIDs.Add(awayPlayerID);
                }
            }
            else
            {
                //error += "Away team lineup not set. Using predicted lineup." + Environment.NewLine;

                //Predict lineup
                SqlParameterCollection sqlParams = new SqlCommand().Parameters;
                sqlParams.Clear();
                sqlParams.AddWithValue("@TeamID", awayTeamID);
                sqlParams.AddWithValue("@PitchHand", homePitcherStats.Tables[0].Rows[0]["PitchHand"]);
                DataSet predictedLineup = DataAdapter.runSPReturnDS(mConnectionString, "pr_GetMostRecentTeamLineupVsPitchHand", sqlParams);
                awayBatterIDs = predictedLineup.Tables[0].AsEnumerable().Select(x => (int)x["PlayerID"]).ToList();

                lblAwayPredictedLineup.Visible = true;
            }

            homeBatterIDs.Clear();
            if (json["dates"][json["dates"].Count - 1].games[0]["lineups"]["homePlayers"] != null)
            {
                foreach (var player in json["dates"][json["dates"].Count - 1].games[0]["lineups"]["homePlayers"])
                {
                    int homePlayerID = player.id;
                    homeBatterIDs.Add(homePlayerID);
                }
            }
            else
            {
                //error += "Home team lineup not set. Using predicted lineup.";

                //Predict lineup
                SqlParameterCollection sqlParams = new SqlCommand().Parameters;
                sqlParams.Clear();
                sqlParams.AddWithValue("@TeamID", homeTeamID);
                sqlParams.AddWithValue("@PitchHand", awayPitcherStats.Tables[0].Rows[0]["PitchHand"]);
                DataSet predictedLineup = DataAdapter.runSPReturnDS(mConnectionString, "pr_GetMostRecentTeamLineupVsPitchHand", sqlParams);
                homeBatterIDs = predictedLineup.Tables[0].AsEnumerable().Select(x => (int)x["PlayerID"]).ToList();

                lblHomePredictedLineup.Visible = true;
            }

            //if (error != "")
            //{
            //    MessageBox.Show(error, "Lineups", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //}
            return error;
        }

        private string LoadAwayPitcher()
        {
            SqlParameterCollection sqlParams = new SqlCommand().Parameters;
            sqlParams.Clear();
            sqlParams.AddWithValue("@PlayerID", awayPitcherID);
            awayPitcherStats = DataAdapter.runSPReturnDS(mConnectionString, "pr_GetPitcherStatsByPlayerID", sqlParams);
            awayPitcherStats.Tables[0].TableName = "Stats";
            awayPitcherStats.Tables[1].TableName = "Probabilities";
            if (awayPitcherStats.Tables["Stats"].Rows.Count == 0)
            {
                dgvAwayPitcher.DataSource = null;
                //MessageBox.Show("Unable to get data for away pitcher (PlayerID #" + awayPitcherID + ")", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                chkHomeBatterPlatoon.Checked = false;
                return "Unable to get data for away pitcher (PlayerID #" + awayPitcherID + ")" + Environment.NewLine;
            }
            chkHomeBatterPlatoon.Text = "Show batting stats/probabilities vs " + awayPitcherStats.Tables[0].Rows[0]["PitchHand"] + "HP";
            dgvAwayPitcher.DataSource = radNumbers.Checked ? awayPitcherStats.Tables["Stats"] : awayPitcherStats.Tables["Probabilities"];
            ((DataTable)dgvAwayPitcher.DataSource).Columns.Add(" ");
            dgvAwayPitcher.Columns[" "].DisplayIndex = 0;
            dgvAwayPitcher.Columns["PlayerID"].Visible = false;
            dgvAwayPitcher.Columns["PitchHand"].HeaderText = "Pitch";
            for (int i = 0; i < dgvAwayPitcher.Columns.Count; i++)
            {
                if (dgvAwayPitcher.Columns[i].Name == " ")
                {
                    dgvAwayPitcher.Columns[i].Width = 25;
                }
                else if (dgvAwayPitcher.Columns[i].Name == "Pitcher")
                {
                    dgvAwayPitcher.Columns[i].Width = 120;
                }
                else
                {
                    dgvAwayPitcher.Columns[i].Width = 38;
                }
            }
            dgvAwayPitcher.Columns["GDP"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            return "";
        }

        private string LoadHomePitcher()
        {
            SqlParameterCollection sqlParams = new SqlCommand().Parameters;
            sqlParams.Clear();
            sqlParams.AddWithValue("@PlayerID", homePitcherID);
            homePitcherStats = DataAdapter.runSPReturnDS(mConnectionString, "pr_GetPitcherStatsByPlayerID", sqlParams);
            homePitcherStats.Tables[0].TableName = "Stats";
            homePitcherStats.Tables[1].TableName = "Probabilities";
            if (homePitcherStats.Tables["Stats"].Rows.Count == 0)
            {
                dgvHomePitcher.DataSource = null;
                //MessageBox.Show("Unable to get data for home pitcher (PlayerID #" + homePitcherID + ")", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                chkAwayBatterPlatoon.Checked = false;
                return "Unable to get data for home pitcher (PlayerID #" + homePitcherID + ")" + Environment.NewLine;
            }
            chkAwayBatterPlatoon.Text = "Show batting stats/probabilities vs " + homePitcherStats.Tables[0].Rows[0]["PitchHand"] + "HP";
            dgvHomePitcher.DataSource = radNumbers.Checked ? homePitcherStats.Tables["Stats"] : homePitcherStats.Tables["Probabilities"];
            ((DataTable)dgvHomePitcher.DataSource).Columns.Add(" ");
            dgvHomePitcher.Columns[" "].DisplayIndex = 0;
            dgvHomePitcher.Columns["PlayerID"].Visible = false;
            dgvHomePitcher.Columns["PitchHand"].HeaderText = "Pitch";
            for (int i = 0; i < dgvHomePitcher.Columns.Count; i++)
            {
                if (dgvHomePitcher.Columns[i].Name == " ")
                {
                    dgvHomePitcher.Columns[i].Width = 25;
                }
                else if (dgvHomePitcher.Columns[i].Name == "Pitcher")
                {
                    dgvHomePitcher.Columns[i].Width = 120;
                }
                else
                {
                    dgvHomePitcher.Columns[i].Width = 38;
                }
            }
            dgvHomePitcher.Columns["GDP"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            return "";
        }

        public string LoadAwayBatters(bool usePlatoonSplits)
        {
            SqlParameterCollection sqlParams = new SqlCommand().Parameters;
            sqlParams.Clear();
            sqlParams.AddWithValue("@TeamID", awayTeamID);
            if (usePlatoonSplits)
            {
                sqlParams.AddWithValue("@PitchHand", homePitcherStats.Tables[0].Rows[0]["PitchHand"]);
            }

            awayBatterStats = DataAdapter.runSPReturnDS(mConnectionString, "pr_GetBatterStatsByTeamID", sqlParams);
            awayBatterStats.Tables[0].TableName = "Stats";
            awayBatterStats.Tables[1].TableName = "Probabilities";
            awayBatterStats.Tables["Stats"].Columns.Add("#");
            awayBatterStats.Tables["Probabilities"].Columns.Add("#");
            dgvAwayBatters.DataSource = radNumbers.Checked ? awayBatterStats.Tables["Stats"] : awayBatterStats.Tables["Probabilities"];
            dgvAwayBatters.Columns["PlayerID"].Visible = false;
            dgvAwayBatters.Columns["#"].DisplayIndex = 0;
            dgvAwayBatters.Columns["BatSide"].HeaderText = "Bat";
            string filter = string.Join(",", awayBatterIDs);
            ((DataTable)dgvAwayBatters.DataSource).DefaultView.RowFilter = "PlayerID IN (" + filter + ")";

            for (int battingNumber = 1; battingNumber <= awayBatterIDs.Count; battingNumber++)
            {
                DataRow batterStats;
                DataRow batterProbabilities;
                batterStats = awayBatterStats.Tables["Stats"].AsEnumerable().SingleOrDefault(x => (int)x["PlayerID"] == awayBatterIDs[battingNumber - 1]);
                batterProbabilities = awayBatterStats.Tables["Probabilities"].AsEnumerable().SingleOrDefault(x => (int)x["PlayerID"] == awayBatterIDs[battingNumber - 1]);

                if (batterStats == null)
                {
                    //The procedure didn't return data for this player. The player must not have any at-bats for his current team.
                    //Get player data by PlayerID. Then, add this datarow to the table that has the rest of this team's data.
                    sqlParams.Clear();
                    sqlParams.AddWithValue("@PlayerID", awayBatterIDs[battingNumber - 1]);
                    DataSet playerBatterStats = DataAdapter.runSPReturnDS(mConnectionString, "pr_GetBatterStatsByPlayerID", sqlParams);
                    playerBatterStats.Tables[0].TableName = "Stats";
                    playerBatterStats.Tables[1].TableName = "Probabilities";
                    if (playerBatterStats.Tables["Stats"].Rows.Count == 0)
                    {
                        dgvAwayBatters.DataSource = null;
                        //MessageBox.Show("Unable to get data for away batter " + battingNumber + " (PlayerID #" + awayBatterIDs[battingNumber - 1] + ")", "Error",
                        //    MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return "Unable to get data for away batter " + battingNumber + " (PlayerID #" + awayBatterIDs[battingNumber - 1] + ")" + Environment.NewLine;
                    }
                    playerBatterStats.Tables["Stats"].Columns.Add("#");
                    playerBatterStats.Tables["Stats"].Rows[0]["#"] = battingNumber;
                    playerBatterStats.Tables["Probabilities"].Columns.Add("#");
                    playerBatterStats.Tables["Probabilities"].Rows[0]["#"] = battingNumber;
                    awayBatterStats.Tables["Stats"].Rows.Add(playerBatterStats.Tables["Stats"].Rows[0].ItemArray);
                    awayBatterStats.Tables["Probabilities"].Rows.Add(playerBatterStats.Tables["Probabilities"].Rows[0].ItemArray);
                }
                else
                {
                    batterStats["#"] = battingNumber;
                    batterProbabilities["#"] = battingNumber;
                }
            }
            ((DataTable)dgvAwayBatters.DataSource).DefaultView.Sort = "#";
            for (int i = 0; i < dgvAwayBatters.Columns.Count; i++)
            {
                if (dgvAwayBatters.Columns[i].Name == "#")
                {
                    dgvAwayBatters.Columns[i].Width = 25;
                }
                else if (dgvAwayBatters.Columns[i].Name == "Batter")
                {
                    dgvAwayBatters.Columns[i].Width = 120;
                }
                else
                {
                    dgvAwayBatters.Columns[i].Width = 38;
                }
            }
            dgvAwayBatters.Columns["GDP"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            return "";
        }

        private string LoadHomeBatters(bool usePlatoonSplits)
        {
            SqlParameterCollection sqlParams = new SqlCommand().Parameters;

            // Home Batters
            sqlParams.Clear();
            sqlParams.AddWithValue("@TeamID", homeTeamID);
            if (usePlatoonSplits)
            {
                sqlParams.AddWithValue("@PitchHand", awayPitcherStats.Tables[0].Rows[0]["PitchHand"]);
            }
            homeBatterStats = DataAdapter.runSPReturnDS(mConnectionString, "pr_GetBatterStatsByTeamID", sqlParams);
            homeBatterStats.Tables[0].TableName = "Stats";
            homeBatterStats.Tables[1].TableName = "Probabilities";
            homeBatterStats.Tables["Stats"].Columns.Add("#");
            homeBatterStats.Tables["Probabilities"].Columns.Add("#");
            dgvHomeBatters.DataSource = radNumbers.Checked ? homeBatterStats.Tables["Stats"] : homeBatterStats.Tables["Probabilities"];
            dgvHomeBatters.Columns["PlayerID"].Visible = false;
            dgvHomeBatters.Columns["#"].DisplayIndex = 0;
            dgvHomeBatters.Columns["BatSide"].HeaderText = "Bat";
            string filter = string.Join(",", homeBatterIDs);
            ((DataTable)dgvHomeBatters.DataSource).DefaultView.RowFilter = "PlayerID IN (" + filter + ")";

            for (int battingNumber = 1; battingNumber <= homeBatterIDs.Count; battingNumber++)
            {
                DataRow batterStats;
                DataRow batterProbabilities;
                batterStats = homeBatterStats.Tables["Stats"].AsEnumerable().SingleOrDefault(x => (int)x["PlayerID"] == homeBatterIDs[battingNumber - 1]);
                batterProbabilities = homeBatterStats.Tables["Probabilities"].AsEnumerable().SingleOrDefault(x => (int)x["PlayerID"] == homeBatterIDs[battingNumber - 1]);
                if (batterStats == null)
                {
                    //The procedure didn't return data for this player. The player must not have any at-bats for his current team.
                    //Get player data by PlayerID. Then, add this datarow to the table that has the rest of this team's data.
                    sqlParams.Clear();
                    sqlParams.AddWithValue("@PlayerID", homeBatterIDs[battingNumber - 1]);
                    DataSet playerBatterStats = DataAdapter.runSPReturnDS(mConnectionString, "pr_GetBatterStatsByPlayerID", sqlParams);
                    playerBatterStats.Tables[0].TableName = "Stats";
                    playerBatterStats.Tables[1].TableName = "Probabilities";
                    if (playerBatterStats.Tables["Stats"].Rows.Count == 0)
                    {
                        dgvHomeBatters.DataSource = null;
                        //MessageBox.Show("Unable to get data for home batter " + battingNumber + " (PlayerID #" + homeBatterIDs[battingNumber - 1] + ")", "Error",
                        //    MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return "Unable to get data for home batter " + battingNumber + " (PlayerID #" + homeBatterIDs[battingNumber - 1] + ")" + Environment.NewLine;
                    }
                    playerBatterStats.Tables["Stats"].Columns.Add("#");
                    playerBatterStats.Tables["Stats"].Rows[0]["#"] = battingNumber;
                    playerBatterStats.Tables["Probabilities"].Columns.Add("#");
                    playerBatterStats.Tables["Probabilities"].Rows[0]["#"] = battingNumber;
                    homeBatterStats.Tables["Stats"].Rows.Add(playerBatterStats.Tables["Stats"].Rows[0].ItemArray);
                    homeBatterStats.Tables["Probabilities"].Rows.Add(playerBatterStats.Tables["Probabilities"].Rows[0].ItemArray);
                }
                else
                {
                    batterStats["#"] = battingNumber;
                    batterProbabilities["#"] = battingNumber;
                }
            }
            ((DataTable)dgvHomeBatters.DataSource).DefaultView.Sort = "#";
            for (int i = 0; i < dgvHomeBatters.Columns.Count; i++)
            {
                if (dgvHomeBatters.Columns[i].Name == "#")
                {
                    dgvHomeBatters.Columns[i].Width = 25;
                }
                else if (dgvHomeBatters.Columns[i].Name == "Batter")
                {
                    dgvHomeBatters.Columns[i].Width = 120;
                }
                else
                {
                    dgvHomeBatters.Columns[i].Width = 38;
                }
            }
            dgvHomeBatters.Columns["GDP"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            return "";
        }

        private void Load1stInningData()
        {
            SqlParameterCollection sqlParams = new SqlCommand().Parameters;
            sqlParams.Clear();
            sqlParams.AddWithValue("@AwayTeamID", awayTeamID);
            sqlParams.AddWithValue("@HomeTeamID", homeTeamID);
            sqlParams.AddWithValue("@AwayPitcherID", awayPitcherID);
            sqlParams.AddWithValue("@HomePitcherID", homePitcherID);
            DataSet matchup1stInning = DataAdapter.runSPReturnDS(mConnectionString, "pr_GetMatchup1stInningData", sqlParams);
            matchup1stInning.Tables[0].TableName = "Team";
            matchup1stInning.Tables[1].TableName = "Pitcher";
        }

        private string[] GetPossibleOutcomes(bool playerOnFirst, bool playerOnSecond, bool playerOnThird, int outs)
        {
            string[] possibleOutcomes = new string[7] { "OUT", "1B", "2B", "3B", "HR", "BB", "HBP" };

            if (playerOnFirst && outs != 2)
            {
                possibleOutcomes.Append("GDP");
            }

            if ((playerOnFirst || playerOnSecond) && outs != 2)
            {
                possibleOutcomes.Append("SF");
            }

            return possibleOutcomes;
        }

        private void PerformOutcome(string outcome, ref bool playerOnFirst, ref bool playerOnSecond, ref bool playerOnThird, ref int outs, ref int runs)
        {
            switch (outcome)
            {
                case "OUT":
                    outs += 1;
                    break;

                case "1B":
                    if (playerOnThird)
                    {
                        playerOnThird = false;
                        runs += 1;
                    }
                    if (playerOnSecond)
                    {
                        playerOnSecond = false;
                        //todo- add prob that runner reached extra base (scored)
                        Random r = new Random();
                        double rand = r.NextDouble() * 100;
                        if (rand > 75)
                        {
                            runs += 1;
                        }
                        else
                        {
                            playerOnThird = true;
                        }
                        //playerOnThird = true;
                    }
                    if (playerOnFirst)
                    {
                        //playerOnSecond = true;
                        //todo- add prob that runner reached extra base (got to 3rd)
                        Random r = new Random();
                        double rand = r.NextDouble() * 100;
                        if (rand > 93)
                        {
                            playerOnThird = true;
                        }
                        else
                        {
                            playerOnSecond = true;
                        }
                    }
                    break;

                case "2B":
                    if (playerOnThird)
                    {
                        playerOnThird = false;
                        runs += 1;
                    }
                    if (playerOnSecond)
                    {
                        runs += 1;
                    }
                    if (playerOnFirst)
                    {
                        playerOnFirst = false;
                        //todo add-prob that runner reached extra base (scored)
                        Random r = new Random();
                        double rand = r.NextDouble() * 100;
                        if (rand > 95)
                        {
                            runs += 1;
                        }
                        else
                        {
                            playerOnThird = true;
                        }
                        //playerOnThird = true;
                    }
                    break;

                case "3B":
                    if (playerOnThird)
                    {
                        runs += 1;
                    }
                    if (playerOnSecond)
                    {
                        playerOnSecond = false;
                        runs += 1;
                    }
                    if (playerOnFirst)
                    {
                        playerOnFirst = false;
                        runs += 1;
                    }
                    break;

                case "HR":
                    if (playerOnThird)
                    {
                        runs += 1;
                        playerOnThird = false;
                    }
                    if (playerOnSecond)
                    {
                        runs += 1;
                        playerOnSecond = false;
                    }
                    if (playerOnFirst)
                    {
                        runs += 1;
                        playerOnFirst = false;
                    }
                    runs += 1;
                    break;

                case "BB":
                case "HBP":
                    if (playerOnFirst)
                    {
                        if (playerOnSecond)
                        {
                            if (playerOnThird)
                            {
                                runs += 1;
                            }
                            else
                            {
                                playerOnThird = true;
                            }
                        }
                        else
                        {
                            playerOnSecond = true;
                        }
                    }
                    else
                    {
                        playerOnFirst = true;
                    }
                    break;

                case "GDP":
                    outs += 2;
                    break;
            }
        }

        private void Simulate()
        {
            double inningsToSimulate = 1.0;

            if (radFirst5Innings.Checked)
            {
                inningsToSimulate = 5.0;
            }
            else if (radEntireGame.Checked)
            {
                inningsToSimulate = 9.0;
            }

            int iterations = Convert.ToInt32(txtIterations.Text);
            double batterWeight = (double)sldWeight.Value / 100;
            double pitcherWeight = 1 - batterWeight;

            //First inning trackers
            int awayTeamScoredIn1stInningCount = 0;
            int homeTeamScoredIn1stInningCount = 0;
            int teamScoredIn1stInningCount = 0;

            //First 5 inning trackers
            var awayTeamFirst5InningsRunsList = new List<int>();
            var homeTeamFirst5InningsRunsList = new List<int>();
            var totalFirst5InningsRunsList = new List<int>();
            int awayTeamFirst5InningsWinCount = 0;
            int homeTeamFirst5InningsWinCount = 0;
            int first5InningsTieCount = 0;

            //Entire game trackers
            var awayTeamRunsList = new List<int>();
            var homeTeamRunsList = new List<int>();
            var totalRunsList = new List<int>();
            int awayTeamWinCount = 0;
            int homeTeamWinCount = 0;
            int tieCount = 0;

            int awayTeamScoredFirstCount = 0;
            int homeTeamScoredFirstCount = 0;

            for (int i = 0; i < iterations; i++)
            {
                //Setup for new game
                double inning = 0;
                bool awayTeamScoredIn1stInning = false;
                bool homeTeamScoredIn1stInning = false;
                bool awayTeamHasScored = false;
                bool homeTeamHasScored = false;
                int awayTeamRuns = 0;
                int homeTeamRuns = 0;

                while (inning < inningsToSimulate)
                {
                    //Setup for new half inning
                    bool playerOnFirst = false;
                    bool playerOnSecond = false;
                    bool playerOnThird = false;
                    int outs = 0;
                    int runs = 0;
                    int battingOrderNumber = 1;

                    while (outs < 3)
                    {
                        //Setup for new batter
                        DataRow batterProbabilities;
                        DataRow pitcherProbabilities;

                        if (inning % 1 == 0)
                        {
                            batterProbabilities = awayBatterStats.Tables["Probabilities"].AsEnumerable().Where(x => x["#"].ToString() == battingOrderNumber.ToString()).SingleOrDefault();

                            if (chkPlatoon.Checked)
                            {
                                if (batterProbabilities["BatSide"].ToString() == "R")
                                {
                                    pitcherProbabilities = homePitcherStats.Tables[3].Rows[0];
                                }
                                else if (batterProbabilities["BatSide"].ToString() == "L")
                                {
                                    pitcherProbabilities = homePitcherStats.Tables[5].Rows[0];
                                }
                                else
                                {
                                    pitcherProbabilities = homePitcherStats.Tables["Probabilities"].Rows[0];
                                }
                            }
                            else
                            {
                                pitcherProbabilities = homePitcherStats.Tables["Probabilities"].Rows[0];
                            }
                        }
                        else
                        {
                            batterProbabilities = homeBatterStats.Tables["Probabilities"].AsEnumerable().Where(x => x["#"].ToString() == battingOrderNumber.ToString()).SingleOrDefault();
                            pitcherProbabilities = awayPitcherStats.Tables["Probabilities"].Rows[0];
                        }

                        string[] possibleOutcomes = GetPossibleOutcomes(playerOnFirst, playerOnSecond, playerOnThird, outs);

                        IWeightedRandomizer<string> randomizer = new DynamicWeightedRandomizer<string>();

                        //handle weights here

                        foreach (string possibleOutcome in possibleOutcomes)
                        {
                            var weightedProbability =
                                ((double)batterProbabilities[possibleOutcome] * batterWeight) + ((double)pitcherProbabilities[possibleOutcome] * pitcherWeight);

                            randomizer.Add(possibleOutcome, (int)(weightedProbability * 100));
                        }

                        string outcome = randomizer.NextWithReplacement();

                        PerformOutcome(outcome, ref playerOnFirst, ref playerOnSecond, ref playerOnThird, ref outs, ref runs);

                        if (battingOrderNumber != 9)
                        {
                            battingOrderNumber += 1;
                        }
                        else
                        {
                            battingOrderNumber = 1;
                        }

                    }
                    //End outs loop. There are 3 outs.             

                    if (runs > 0)
                    {
                        if (inning % 1 == 0)
                        {
                            //Bottom of the inning. Away team scored.
                            if (!awayTeamHasScored && !homeTeamHasScored)
                            {
                                awayTeamScoredFirstCount += 1;
                                awayTeamHasScored = true;
                            }
                            awayTeamRuns += runs;
                            if (inning == 0)
                            {
                                awayTeamScoredIn1stInning = true;
                                awayTeamScoredIn1stInningCount += 1;
                            }
                        }
                        else
                        {
                            //Top of inning. Home team scored.
                            if (!awayTeamHasScored && !homeTeamHasScored)
                            {
                                homeTeamScoredFirstCount += 1;
                                homeTeamHasScored = true;
                            }
                            homeTeamRuns += runs;
                            if (inning == 0.5)
                            {
                                homeTeamScoredIn1stInning = true;
                                homeTeamScoredIn1stInningCount += 1;
                            }
                        }
                    }

                    inning += 0.5;

                    if (inning == 5.0)
                    {
                        //First 5 innings have completed
                        awayTeamFirst5InningsRunsList.Add(awayTeamRuns);
                        homeTeamFirst5InningsRunsList.Add(homeTeamRuns);
                        totalFirst5InningsRunsList.Add(awayTeamRuns + homeTeamRuns);

                        if (awayTeamRuns > homeTeamRuns)
                        {
                            awayTeamFirst5InningsWinCount += 1;
                        }
                        else if (homeTeamRuns > awayTeamRuns)
                        {
                            homeTeamFirst5InningsWinCount += 1;
                        }
                        else
                        {
                            first5InningsTieCount += 1;
                        }
                    }
                }
                //End game loop. The game has finished

                if (awayTeamScoredIn1stInning || homeTeamScoredIn1stInning)
                {
                    teamScoredIn1stInningCount += 1;
                }

                awayTeamRunsList.Add(awayTeamRuns);
                homeTeamRunsList.Add(homeTeamRuns);
                totalRunsList.Add(awayTeamRuns + homeTeamRuns);

                if (awayTeamRuns > homeTeamRuns)
                {
                    awayTeamWinCount += 1;
                }
                else if (homeTeamRuns > awayTeamRuns)
                {
                    homeTeamWinCount += 1;
                }
                else
                {
                    tieCount += 1;
                    //todo- actually simulate extra innings instead of this
                }

            }
            //End iterations loop. All n games have finished.

            string awayTeam = Convert.ToString(matchup["AwayTeamName"]);
            string homeTeam = Convert.ToString(matchup["HomeTeamName"]);
            //string awayTeam = schedule.Tables[0].Rows[cboMatchups.SelectedIndex]["AwayTeamName"].ToString();
            //string homeTeam = schedule.Tables[0].Rows[cboMatchups.SelectedIndex]["HomeTeamName"].ToString();

            //Run scored in first inning
            double awayTeamScoredPct = (double)awayTeamScoredIn1stInningCount / (double)iterations * 100;
            double homeTeamScoredPct = (double)homeTeamScoredIn1stInningCount / (double)iterations * 100;
            double firstInningScoredPct = (double)teamScoredIn1stInningCount / (double)iterations * 100;
            double firstInningNoScorePct = 100.0 - firstInningScoredPct;
            string firstInningScoredOdds = ConvertProbabilityToAmericanOdds(firstInningScoredPct);
            string firstInningNoScoreOdds = ConvertProbabilityToAmericanOdds(firstInningNoScorePct);
            lblRunScoredInFirstInningYesOdds.Text = firstInningScoredPct + "% (" + firstInningScoredOdds + ")";
            lblRunScoredInFirstInningNoOdds.Text = firstInningNoScorePct + "% (" + firstInningNoScoreOdds + ")";

            //Team to score first
            double awayTeamScoredFirstPct = (double)awayTeamScoredFirstCount / (double)iterations * 100.0;
            double homeTeamScoredFirstPct = (double)homeTeamScoredFirstCount / (double)iterations * 100.0;
            string awayTeamScoredFirstOdds = ConvertProbabilityToAmericanOdds(awayTeamScoredFirstPct);
            string homeTeamScoredFirstOdds = ConvertProbabilityToAmericanOdds(homeTeamScoredFirstPct);
            lblTeamToScoreFirstAwayOdds.Text = awayTeamScoredFirstPct + "% (" + awayTeamScoredFirstOdds + ")";
            lblTeamToScoreFirstHomeOdds.Text = homeTeamScoredFirstPct + "% (" + homeTeamScoredFirstOdds + ")";

            if (inningsToSimulate >= 5.0)
            {
                //Setup First 5 Innings data and grid

                //Spread
                double awayTeamFirst5InningsWinPct = (double)awayTeamFirst5InningsWinCount / (double)iterations * 100.0;
                double homeTeamFirst5InningsWinPct = (double)homeTeamFirst5InningsWinCount / (double)iterations * 100.0;
                int awayTeamFirst5InningsWonTheSpreadCount = 0;
                int homeTeamFirst5InningsWonTheSpreadCount = 0;
                if (awayTeamFirst5InningsWinPct > homeTeamFirst5InningsWinPct)
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        if (awayTeamFirst5InningsRunsList[i] > homeTeamFirst5InningsRunsList[i])
                        {
                            awayTeamFirst5InningsWonTheSpreadCount += 1;
                        }
                        else
                        {
                            homeTeamFirst5InningsWonTheSpreadCount += 1;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        if (homeTeamFirst5InningsRunsList[i] > awayTeamFirst5InningsRunsList[i])
                        {
                            homeTeamFirst5InningsWonTheSpreadCount += 1;
                        }
                        else
                        {
                            awayTeamFirst5InningsWonTheSpreadCount += 1;
                        }
                    }
                }
                double awayTeamFirst5InningsWonTheSpreadPct = (double)awayTeamFirst5InningsWonTheSpreadCount / (double)iterations * 100.0;
                double homeTeamFirst5InningsWonTheSpreadPct = (double)homeTeamFirst5InningsWonTheSpreadCount / (double)iterations * 100.0;
                string awayTeamFirst5InningsWonTheSpreadOdds = ConvertProbabilityToAmericanOdds(awayTeamFirst5InningsWonTheSpreadPct);
                string homeTeamFirst5InningsWonTheSpreadOdds = ConvertProbabilityToAmericanOdds(homeTeamFirst5InningsWonTheSpreadPct);

                //Moneyline
                double first5InningsTiePct = (double)first5InningsTieCount / (double)iterations * 100;
                string awayTeamFirst5InningsWinOdds = ConvertProbabilityToAmericanOdds(awayTeamFirst5InningsWinPct);
                string homeTeamFirst5InningsWinOdds = ConvertProbabilityToAmericanOdds(homeTeamFirst5InningsWinPct);

                //Total runs
                double averageFirst5InningsTotalRoundedToHalf;
                double averageFirst5InningsTotalRoundedToWhole = Math.Round(totalFirst5InningsRunsList.Average(), 0);
                if (averageFirst5InningsTotalRoundedToWhole > totalFirst5InningsRunsList.Average())
                {
                    averageFirst5InningsTotalRoundedToHalf = averageFirst5InningsTotalRoundedToWhole - 0.5;
                }
                else
                {
                    averageFirst5InningsTotalRoundedToHalf = averageFirst5InningsTotalRoundedToWhole + 0.5;
                }
                double totalFirst5InningsRunsOverAverageTotalRoundedToHalfPct = totalFirst5InningsRunsList.Count(x => x > averageFirst5InningsTotalRoundedToHalf) / (double)iterations * 100.0;
                double totalFirst5InningsRunsOverAverageTotalRoundedToWholePct = totalFirst5InningsRunsList.Count(x => x > averageFirst5InningsTotalRoundedToWhole) / (double)iterations * 100.0;
                string totalFirst5InningsRunsOverAverageTotalRoundedToHalfOdds = ConvertProbabilityToAmericanOdds(totalFirst5InningsRunsOverAverageTotalRoundedToHalfPct);
                string totalFirst5InningsRunsOverAverageTotalRoundedToWholeOdds = ConvertProbabilityToAmericanOdds(totalFirst5InningsRunsOverAverageTotalRoundedToWholePct);

                double totalFirst5InningsRunsUnderAverageTotalRoundedToHalfPct = totalFirst5InningsRunsList.Count(x => x < averageFirst5InningsTotalRoundedToHalf) / (double)iterations * 100.0;
                double totalFirst5InningsRunsUnderAverageTotalRoundedToWholePct = totalFirst5InningsRunsList.Count(x => x < averageFirst5InningsTotalRoundedToWhole) / (double)iterations * 100.0;
                string totalFirst5InningsRunsUnderAverageTotalRoundedToHalfOdds = ConvertProbabilityToAmericanOdds(totalFirst5InningsRunsUnderAverageTotalRoundedToHalfPct);
                string totalFirst5InningsRunsUnderAverageTotalRoundedToWholeOdds = ConvertProbabilityToAmericanOdds(totalFirst5InningsRunsUnderAverageTotalRoundedToWholePct);

                //Datagrid
                DataTable first5Innings = new DataTable();
                first5Innings.Columns.Add("Team");
                first5Innings.Columns.Add("Spread");
                first5Innings.Columns.Add("Moneyline");
                first5Innings.Columns.Add("Total #1");
                first5Innings.Columns.Add("Total #2");
                DataRow first5InningsAway = first5Innings.NewRow();
                first5InningsAway["Team"] = awayTeam;
                first5InningsAway["Spread"] = (awayTeamFirst5InningsWinPct > homeTeamFirst5InningsWinPct ? "-0.5   " : "+0.5   ") + awayTeamFirst5InningsWonTheSpreadPct + "% (" + awayTeamFirst5InningsWonTheSpreadOdds + ")";
                first5InningsAway["Moneyline"] = awayTeamFirst5InningsWinPct + "% (" + awayTeamFirst5InningsWinOdds + ")";
                if (averageFirst5InningsTotalRoundedToHalf < averageFirst5InningsTotalRoundedToWhole)
                {
                    first5InningsAway["Total #1"] = "Ov " + averageFirst5InningsTotalRoundedToHalf + "   " + totalFirst5InningsRunsOverAverageTotalRoundedToHalfPct + "% (" + totalFirst5InningsRunsOverAverageTotalRoundedToHalfOdds + ")";
                    first5InningsAway["Total #2"] = "Ov " + averageFirst5InningsTotalRoundedToWhole + "   " + totalFirst5InningsRunsOverAverageTotalRoundedToWholePct + "% (" + totalFirst5InningsRunsOverAverageTotalRoundedToWholeOdds + ")";
                }
                else
                {
                    first5InningsAway["Total #1"] = "Ov " + averageFirst5InningsTotalRoundedToWhole + "   " + totalFirst5InningsRunsOverAverageTotalRoundedToWholePct + "% (" + totalFirst5InningsRunsOverAverageTotalRoundedToWholeOdds + ")";
                    first5InningsAway["Total #2"] = "Ov " + averageFirst5InningsTotalRoundedToHalf + "   " + totalFirst5InningsRunsOverAverageTotalRoundedToHalfPct + "% (" + totalFirst5InningsRunsOverAverageTotalRoundedToHalfOdds + ")";
                }
                first5Innings.Rows.Add(first5InningsAway);
                DataRow first5InningsHome = first5Innings.NewRow();
                first5InningsHome["Team"] = homeTeam;
                first5InningsHome["Spread"] = (awayTeamFirst5InningsWinPct <= homeTeamFirst5InningsWinPct ? "-0.5   " : "+0.5   ") + homeTeamFirst5InningsWonTheSpreadPct + "% (" + homeTeamFirst5InningsWonTheSpreadOdds + ")";
                first5InningsHome["Moneyline"] = homeTeamFirst5InningsWinPct + "% (" + homeTeamFirst5InningsWinOdds + ")";
                if (averageFirst5InningsTotalRoundedToHalf < averageFirst5InningsTotalRoundedToWhole)
                {
                    first5InningsHome["Total #1"] = "Un " + averageFirst5InningsTotalRoundedToHalf + "   " + totalFirst5InningsRunsUnderAverageTotalRoundedToHalfPct + "% (" + totalFirst5InningsRunsUnderAverageTotalRoundedToHalfOdds + ")";
                    first5InningsHome["Total #2"] = "Un " + averageFirst5InningsTotalRoundedToWhole + "   " + totalFirst5InningsRunsUnderAverageTotalRoundedToWholePct + "% (" + totalFirst5InningsRunsUnderAverageTotalRoundedToWholeOdds + ")";
                }
                else
                {
                    first5InningsHome["Total #1"] = "Un " + averageFirst5InningsTotalRoundedToWhole + "   " + totalFirst5InningsRunsUnderAverageTotalRoundedToWholePct + "% (" + totalFirst5InningsRunsUnderAverageTotalRoundedToWholeOdds + ")";
                    first5InningsHome["Total #2"] = "Un " + averageFirst5InningsTotalRoundedToHalf + "   " + totalFirst5InningsRunsUnderAverageTotalRoundedToHalfPct + "% (" + totalFirst5InningsRunsUnderAverageTotalRoundedToHalfOdds + ")";
                }
                first5Innings.Rows.Add(first5InningsHome);
                dgvFirst5Innings.DataSource = first5Innings;
                for (int i = 0; i < dgvFirst5Innings.Columns.Count; i++)
                {
                    dgvFirst5Innings.Columns[i].Width = 140;
                }
                dgvFirst5Innings.Columns["Total #2"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }

            if (inningsToSimulate == 9.0)
            {
                //Setup Entire Game data and grid

                //Spread
                double awayTeamWinPct = (double)awayTeamWinCount / (double)iterations * 100.0;
                double homeTeamWinPct = (double)homeTeamWinCount / (double)iterations * 100.0;
                int awayTeamWonTheSpreadCount = 0;
                int homeTeamWonTheSpreadCount = 0;
                if (awayTeamWinPct > homeTeamWinPct)
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        if (awayTeamRunsList[i] > homeTeamRunsList[i] + 1.5)
                        {
                            awayTeamWonTheSpreadCount += 1;
                        }
                        else
                        {
                            homeTeamWonTheSpreadCount += 1;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        if (homeTeamRunsList[i] > awayTeamRunsList[i] + 1.5)
                        {
                            homeTeamWonTheSpreadCount += 1;
                        }
                        else
                        {
                            awayTeamWonTheSpreadCount += 1;
                        }
                    }
                }

                double awayTeamWonTheSpreadPct = (double)awayTeamWonTheSpreadCount / (double)iterations * 100.0;
                double homeTeamWonTheSpreadPct = (double)homeTeamWonTheSpreadCount / (double)iterations * 100.0;
                string awayTeamWonTheSpreadOdds = ConvertProbabilityToAmericanOdds(awayTeamWonTheSpreadPct);
                string homeTeamWonTheSpreadOdds = ConvertProbabilityToAmericanOdds(homeTeamWonTheSpreadPct);

                //Moneyline
                double tiePct = (double)tieCount / (double)iterations * 100;
                string awayTeamWinOdds = ConvertProbabilityToAmericanOdds(awayTeamWinPct);
                string homeTeamWinOdds = ConvertProbabilityToAmericanOdds(homeTeamWinPct);

                //Total runs
                double averageTotalRoundedToHalf;
                double averageTotalRoundedToWhole = Math.Round(totalRunsList.Average(), 0);
                if (averageTotalRoundedToWhole > totalRunsList.Average())
                {
                    averageTotalRoundedToHalf = averageTotalRoundedToWhole - 0.5;
                }
                else
                {
                    averageTotalRoundedToHalf = averageTotalRoundedToWhole + 0.5;
                }
                double totalRunsOverAverageTotalRoundedToHalfPct = totalRunsList.Count(x => x > averageTotalRoundedToHalf) / (double)iterations * 100.0;
                double totalRunsOverAverageTotalRoundedToWholePct = totalRunsList.Count(x => x > averageTotalRoundedToWhole) / (double)iterations * 100.0;
                string totalRunsOverAverageTotalRoundedToHalfOdds = ConvertProbabilityToAmericanOdds(totalRunsOverAverageTotalRoundedToHalfPct);
                string totalRunsOverAverageTotalRoundedToWholeOdds = ConvertProbabilityToAmericanOdds(totalRunsOverAverageTotalRoundedToWholePct);

                double totalRunsUnderAverageTotalRoundedToHalfPct = totalRunsList.Count(x => x < averageTotalRoundedToHalf) / (double)iterations * 100.0;
                double totalRunsUnderAverageTotalRoundedToWholePct = totalRunsList.Count(x => x < averageTotalRoundedToWhole) / (double)iterations * 100.0;
                string totalRunsUnderAverageTotalRoundedToHalfOdds = ConvertProbabilityToAmericanOdds(totalRunsUnderAverageTotalRoundedToHalfPct);
                string totalRunsUnderAverageTotalRoundedToWholeOdds = ConvertProbabilityToAmericanOdds(totalRunsUnderAverageTotalRoundedToWholePct);

                //Datagrid
                DataTable entireGame = new DataTable();
                entireGame.Columns.Add("Team");
                entireGame.Columns.Add("Spread");
                entireGame.Columns.Add("Moneyline");
                entireGame.Columns.Add("Total #1");
                entireGame.Columns.Add("Total #2");
                DataRow entireGameAway = entireGame.NewRow();
                entireGameAway["Team"] = awayTeam;
                entireGameAway["Spread"] = (awayTeamWinPct > homeTeamWinPct ? "-01.5   " : "+1.5   ") + awayTeamWonTheSpreadPct + "% (" + awayTeamWonTheSpreadOdds + ")";
                entireGameAway["Moneyline"] = awayTeamWinPct + "% (" + awayTeamWinOdds + ")";
                if (averageTotalRoundedToHalf < averageTotalRoundedToWhole)
                {
                    entireGameAway["Total #1"] = "Ov " + averageTotalRoundedToHalf + "   " + totalRunsOverAverageTotalRoundedToHalfPct + "% (" + totalRunsOverAverageTotalRoundedToHalfOdds + ")";
                    entireGameAway["Total #2"] = "Ov " + averageTotalRoundedToWhole + "   " + totalRunsOverAverageTotalRoundedToWholePct + "% (" + totalRunsOverAverageTotalRoundedToWholeOdds + ")";
                }
                else
                {
                    entireGameAway["Total #1"] = "Ov " + averageTotalRoundedToWhole + "   " + totalRunsOverAverageTotalRoundedToWholePct + "% (" + totalRunsOverAverageTotalRoundedToWholeOdds + ")";
                    entireGameAway["Total #2"] = "Ov " + averageTotalRoundedToHalf + "   " + totalRunsOverAverageTotalRoundedToHalfPct + "% (" + totalRunsOverAverageTotalRoundedToHalfOdds + ")";
                }
                entireGame.Rows.Add(entireGameAway);
                DataRow entireGameHome = entireGame.NewRow();
                entireGameHome["Team"] = homeTeam;
                entireGameHome["Spread"] = (awayTeamWinPct <= homeTeamWinPct ? "-1.5   " : "+1.5   ") + homeTeamWonTheSpreadPct + "% (" + homeTeamWonTheSpreadOdds + ")";
                entireGameHome["Moneyline"] = homeTeamWinPct + "% (" + homeTeamWinOdds + ")";
                if (averageTotalRoundedToHalf < averageTotalRoundedToWhole)
                {
                    entireGameHome["Total #1"] = "Un " + averageTotalRoundedToHalf + "   " + totalRunsUnderAverageTotalRoundedToHalfPct + "% (" + totalRunsUnderAverageTotalRoundedToHalfOdds + ")";
                    entireGameHome["Total #2"] = "Un " + averageTotalRoundedToWhole + "   " + totalRunsUnderAverageTotalRoundedToWholePct + "% (" + totalRunsUnderAverageTotalRoundedToWholeOdds + ")";
                }
                else
                {
                    entireGameHome["Total #1"] = "Un " + averageTotalRoundedToWhole + "   " + totalRunsUnderAverageTotalRoundedToWholePct + "% (" + totalRunsUnderAverageTotalRoundedToWholeOdds + ")";
                    entireGameHome["Total #2"] = "Un " + averageTotalRoundedToHalf + "   " + totalRunsUnderAverageTotalRoundedToHalfPct + "% (" + totalRunsUnderAverageTotalRoundedToHalfOdds + ")";
                }
                entireGame.Rows.Add(entireGameHome);
                dgvEntireGame.DataSource = entireGame;
                for (int i = 0; i < dgvEntireGame.Columns.Count; i++)
                {
                    dgvEntireGame.Columns[i].Width = 140;
                }
                dgvEntireGame.Columns["Total #2"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
            else
            {
                dgvEntireGame.DataSource = null;
            }
        }

        public string ConvertProbabilityToAmericanOdds(double probability)
        {
            string prefix;
            int odds;
            if (probability >= 50.0)
            {
                prefix = "-";
                odds = (int)(probability / (100.0 - probability) * 100.0);
            }
            else
            {
                prefix = "+";
                odds = (int)((100.0 - probability) / probability * 100.0);
            }

            return prefix + odds;
        }

        #endregion

        private void DgvHomePitcher_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvHomePitcher.Columns[e.ColumnIndex].Name == "PA")
            {
                if (Convert.ToInt32(e.Value) < 100)
                {
                    e.CellStyle.BackColor = Color.Yellow;
                }
            }
        }

        private void DgvAwayPitcher_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvAwayPitcher.Columns[e.ColumnIndex].Name == "PA")
            {
                if (Convert.ToInt32(e.Value) < 100)
                {
                    e.CellStyle.BackColor = Color.Yellow;
                }
            }
        }

        private void DgvAwayBatters_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvAwayBatters.Columns[e.ColumnIndex].Name == "PA")
            {
                if (Convert.ToInt32(e.Value) < 100)
                {
                    e.CellStyle.BackColor = Color.Yellow;
                }
            }
        }

        private void DgvHomeBatters_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvHomeBatters.Columns[e.ColumnIndex].Name == "PA")
            {
                if (Convert.ToInt32(e.Value) < 100)
                {
                    e.CellStyle.BackColor = Color.Yellow;
                }
            }
        }

        private int GetAdjustedWindBearing(int windBearing, int batterBearing)
        {
            int adjustedBearing;

            if (windBearing > batterBearing)
            {
                adjustedBearing = windBearing - batterBearing;
            }
            else
            {
                adjustedBearing = windBearing + (360 - batterBearing);
            }

            return adjustedBearing;
        }

        private void GetOddsData()
        {
            var scraper = new OddsScraper();
            scraper.Start();
        }

        private void grpMatchup_Click(object sender, EventArgs e)
        {
            //Console.WriteLine(((GroupBox)sender).Name);

            int gameId;

            if (sender is GroupBox)
            {
                var ctrl = (GroupBox)sender;
                int output;
                if (Int32.TryParse(ctrl.Name, out output))
                {
                    gameId = Convert.ToInt32(ctrl.Name);
                }
                else
                {
                    return;
                }
                
                //ctrl.BackColor = Color.White;

                //foreach (var child in ctrl.Controls)
                //{
                //    if (child is Label)
                //    {
                //        ((Label)child).BackColor = Color.White;
                //    }
                //}
                
            }
            else if (sender is Label)
            {
                gameId = Convert.ToInt32(((Label)sender).Name);
            }
            else
            {
                gameId = Convert.ToInt32(((PictureBox)sender).Name);
            }

            dgvAwayPitcher.DataSource = null;
            dgvHomePitcher.DataSource = null;
            dgvAwayBatters.DataSource = null;
            dgvHomeBatters.DataSource = null;
            dgvFirst5Innings.DataSource = null;
            dgvEntireGame.DataSource = null;

            matchup = schedule.Tables[0].AsEnumerable().Where(x => (int)x["MlbGameID"] == gameId).SingleOrDefault();
            awayTeamID = (int)matchup["AwayTeamID"];
            homeTeamID = (int)matchup["HomeTeamID"];
            venueID = (int)matchup["MlbVenueID"];

            string awayTeam = Convert.ToString(matchup["AwayTeamAbbrev"]); //schedule.Tables[0].Rows[cboMatchups.SelectedIndex]["AwayTeamName"].ToString();
            string homeTeam = Convert.ToString(matchup["HomeTeamAbbrev"]); //schedule.Tables[0].Rows[cboMatchups.SelectedIndex]["HomeTeamName"].ToString();

            Image imgAway = Image.FromFile("C:\\Desktop\\mlb_model\\mlb_model\\img\\team_logos\\" + Convert.ToString(matchup["AwayTeamAbbrev"]) + ".png");
            Image imghome = Image.FromFile("C:\\Desktop\\mlb_model\\mlb_model\\img\\team_logos\\" + Convert.ToString(matchup["HomeTeamAbbrev"]) + ".png");

            picAwayLogo.Image = imgAway;   
            picHomeLogo.Image = imghome;
            lblMatchup.Text = awayTeam + " @ " + homeTeam;

            lblRunScoredInFirstInningNoOdds.Text = "";
            lblRunScoredInFirstInningYesOdds.Text = "";
            lblTeamToScoreFirstAway.Text = awayTeam;
            lblTeamToScoreFirstHome.Text = homeTeam;
            lblTeamToScoreFirstAwayOdds.Text = "";
            lblTeamToScoreFirstHomeOdds.Text = "";

            string error = "";

            GetVenueData();
            GetWeatherData();

            error += GetProbablePitchers(gameId);
            if (error == "")
            {
                error += LoadAwayPitcher();
                error += LoadHomePitcher();
                error += GetLineups(gameId);
                error += LoadAwayBatters(chkAwayBatterPlatoon.Checked);
                error += LoadHomeBatters(chkHomeBatterPlatoon.Checked);
                Load1stInningData();
            }

            if (error != "")
            {
                MessageBox.Show(error, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            //GetOddsData();
        }

        private void Test(int gameId)
        {
            Console.WriteLine(gameId);
        }

    }

    class OddsScraper : WebScraper
    {
        public override void Init()
        {
                this.LoggingLevel = WebScraper.LogLevel.All;
                this.Request("https://www.sportsbookreview.com/betting-odds/mlb-baseball/", Parse);
        }

        public override void Parse(Response response)
        {
            foreach (var title_link in response.Css("h2.entry-title a"))
            {
                string strTitle = title_link.TextContentClean;
                Scrape(new ScrapedData() { { "Title", strTitle } });
            }
            if (response.CssExists("div.prev-post > a[href]"))
            {
                var next_page = response.Css("div.prev-post > a[href]")[0].Attributes["href"];
                this.Request(next_page, Parse);
            }
        }
    }
}

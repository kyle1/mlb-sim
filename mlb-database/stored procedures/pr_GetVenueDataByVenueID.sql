USE [baseball]
GO

/****** Object:  StoredProcedure [dbo].[pr_GetVenueDataByVenueID]    Script Date: 8/25/2019 9:34:17 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[pr_GetVenueDataByVenueID] 
    @VenueID INT
AS

SELECT
	VenuePlus.VenueID,
	VenuePlus.Name,
	HomeTeamRunsScored = SUM(Game.HomeTeamScore),
	HomeTeamRunsAllowed = SUM(Game.AwayTeamScore),
	GameCount = COUNT(*),
	Numerator = CAST(SUM(Game.HomeTeamScore + Game.AwayTeamScore) AS FLOAT) / CAST(COUNT(*) AS FLOAT),
	VenuePlus.Latitude,
	VenuePlus.Longitude,
	VenuePlus.BatterBearing
INTO #HomeGames
FROM VenuePlus
JOIN Team on Team.VenueID = VenuePlus.VenueID
JOIN Game ON Game.HomeTeamID = Team.TeamID
WHERE
	VenuePlus.VenueID = @VenueID
GROUP BY
	VenuePlus.VenueID,
	VenuePlus.Name,
	VenuePlus.Latitude,
	VenuePlus.Longitude,
	VenuePlus.BatterBearing

SELECT
	VenuePlus.VenueID,
	VenuePlus.Name,
	AwayTeamRunsScored = SUM(Game.AwayTeamScore),
	AwayTeamRunsAllowed = SUM(Game.HomeTeamScore),
	GameCount = COUNT(*),
	Denominator = CAST(SUM(Game.AwayTeamScore + Game.HomeTeamScore) AS FLOAT) / CAST(COUNT(*) AS FLOAT),
	VenuePlus.Latitude,
	VenuePlus.Longitude,
	VenuePlus.BatterBearing
INTO #AwayGames
FROM VenuePlus
JOIN Team on Team.VenueID = VenuePlus.VenueID
JOIN Game ON Game.AwayTeamID = Team.TeamID
WHERE
	VenuePlus.VenueID = @VenueID
GROUP BY
	VenuePlus.VenueID,
	VenuePlus.Name,
	VenuePlus.Latitude,
	VenuePlus.Longitude,
	VenuePlus.BatterBearing

SELECT
	#HomeGames.VenueID,
	#HomeGames.Name,
	ParkFactor = #HomeGames.Numerator / #AwayGames.Denominator,
	#HomeGames.Latitude,
	#HomeGames.Longitude,
	#HomeGames.BatterBearing
FROM #HomeGames
JOIN #AwayGames ON #HomeGames.VenueID = #AwayGames.VenueID
ORDER BY ParkFactor DESC

DROP TABLE
	#HomeGames,
	#AwayGames
GO



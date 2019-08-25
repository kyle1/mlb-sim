USE [baseball]
GO

/****** Object:  StoredProcedure [dbo].[pr_GetGamesByDate]    Script Date: 8/25/2019 9:32:09 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO




CREATE PROCEDURE [dbo].[pr_GetGamesByDate] 
    @GameDate DATE
AS


SELECT --TOP 25
	GameDate = CONVERT(DATE, MlbSchedule.GameDate),
	GameDateTimeUTC = CONVERT(DATETIME2,MlbSchedule.GameDate,127),
	GameDateTimePST = DATEADD(HOUR, -7, CONVERT(DATETIME2,MlbSchedule.GameDate,127)),
	--GameTimePST = CONVERT(VARCHAR(15), DATEADD(HOUR, -7, CAST(MlbSchedule.GameDate AS TIME)), 100),
	GameTimePST = FORMAT(DATEADD(HOUR, -7, CONVERT(DATETIME2,MlbSchedule.GameDate,127)), 'h:mm tt'),
	MlbSchedule.MlbGameID,
	AwayTeamID = Away.TeamID,
	AwayTeamAbbrev = Away.Abbreviation,
	HomeTeamID = Home.TeamID,
	HomeTeamAbbrev = Home.Abbreviation,
	Matchup = Away.Abbreviation + ' @ ' + Home.Abbreviation + ' (' + FORMAT(DATEADD(HOUR, -7, CONVERT(DATETIME2,MlbSchedule.GameDate,127)), 'h:mm tt') + ')',
	MlbSchedule.MlbVenueID
FROM MlbSchedule
JOIN Team Away ON MlbSchedule.AwayTeamID = Away.TeamID
JOIN Team Home ON MlbSchedule.HomeTeamID = Home.TeamID
WHERE
	CONVERT(DATE, DATEADD(HOUR, -7, CONVERT(DATETIME2,MlbSchedule.GameDate,127))) = CONVERT(DATE, @GameDate)
ORDER BY
	MlbSchedule.GameDate


	select * from team
GO



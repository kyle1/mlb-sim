USE [baseball]
GO

/****** Object:  StoredProcedure [dbo].[pr_GetMatchup1stInningData]    Script Date: 8/25/2019 9:32:21 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO




CREATE PROCEDURE [dbo].[pr_GetMatchup1stInningData] 
	@AwayTeamID			INT,
	@HomeTeamID			INT,
	@AwayPitcherID		INT,
	@HomePitcherID		INT
AS


/****************** TEAM ******************/
SELECT
	AwayAndHome.*
INTO #Team1stInning
FROM (
	SELECT
		Team.TeamID,
		Team.Name,
		Game.GameDate,
		Game.GameID,
		HomeOrAway = 'Away',
		Scored = 1
			--CASE
			--	WHEN SUM(CASE WHEN IsScoringPlay = 1 THEN 1 ELSE 0 END) > 0
			--	THEN 1
			--	ELSE 0
			--END
	FROM Team
	JOIN Game ON Team.TeamID = Game.AwayTeamID
	JOIN PlayByPlay ON Game.GameID = PlayByPlay.GameID
	WHERE
		Team.TeamID IN (@AwayTeamID, @HomeTeamID) AND
		PlayByPlay.HalfInning = 'top' AND
		PlayByPlay.Inning = 1
	GROUP BY
		Team.TeamID,
		Team.Name,
		Game.GameDate,
		Game.GameID

	UNION ALL

	SELECT
		Team.TeamID,
		Team.Name,
		Game.GameDate,
		Game.GameID,
		HomeOrAway = 'Home',
		Scored = 1
			--CASE
			--	WHEN SUM(CASE WHEN IsScoringPlay = 1 THEN 1 ELSE 0 END) > 0
			--	THEN 1
			--	ELSE 0
			--END
	FROM Team
	JOIN Game ON Team.TeamID = Game.HomeTeamID
	JOIN PlayByPlay ON Game.GameID = PlayByPlay.GameID
	WHERE
		Team.TeamID IN (@AwayTeamID, @HomeTeamID) AND
		PlayByPlay.HalfInning = 'bottom' AND
		PlayByPlay.Inning = 1
	GROUP BY
		Team.TeamID,
		Team.Name,
		Game.GameDate,
		Game.GameID
	) AwayAndHome

SELECT
	#Team1stInning.TeamID,
	#Team1stInning.Name,
	GameCount = COUNT(*),
	ScoredCount = SUM(#Team1stInning.Scored),
	ScoredPct = ROUND(CAST(SUM(#Team1stInning.Scored) AS FLOAT) / CAST(COUNT(*) AS FLOAT), 2),
	HomeGameCount = COUNT(DISTINCT CASE WHEN #Team1stInning.HomeOrAway = 'Home' THEN #Team1stInning.GameID ELSE NULL END),
	HomeScoredCount = SUM(CASE WHEN #Team1stInning.HomeOrAway =  'Home' THEN #Team1stInning.Scored ELSE 0 END),
	HomeScoredPct  = ROUND(CAST(SUM(CASE WHEN #Team1stInning.HomeOrAway =  'Home' THEN #Team1stInning.Scored ELSE 0 END) AS FLOAT) /
						CAST(COUNT(DISTINCT CASE WHEN #Team1stInning.HomeOrAway = 'Home' THEN #Team1stInning.GameID ELSE NULL END) AS FLOAT), 2),
	AwayGameCount = COUNT(DISTINCT CASE WHEN #Team1stInning.HomeOrAway = 'Away' THEN #Team1stInning.GameID ELSE NULL END),
	AwayScoredCount = SUM(CASE WHEN #Team1stInning.HomeOrAway =  'Away' THEN #Team1stInning.Scored ELSE 0 END),
	AwayScoredPct  = ROUND(CAST(SUM(CASE WHEN #Team1stInning.HomeOrAway =  'Away' THEN #Team1stInning.Scored ELSE 0 END) AS FLOAT) /
						CAST(COUNT(DISTINCT CASE WHEN #Team1stInning.HomeOrAway = 'Away' THEN #Team1stInning.GameID ELSE NULL END) AS FLOAT), 2)
FROM #Team1stInning
GROUP BY
	#Team1stInning.TeamID,
	#Team1stInning.Name
ORDER BY
	ScoredPct DESC


/****************** PITCHER ******************/
SELECT
	Pitcher.PlayerID,
	Pitcher.FullName,
	Game.GameDate,
	Game.GameID,
	Scored =
			CASE
				WHEN SUM(CASE WHEN IsScoringPlay = 1 THEN 1 ELSE 0 END) > 0
				THEN 1
				ELSE 0
			END
INTO #Pitcher1stInning
FROM Player Pitcher
JOIN PlayByPlay ON Pitcher.PlayerID = PlayByPlay.PitcherID
JOIN Game ON PlayByPlay.GameID = Game.GameID
WHERE
	Pitcher.PlayerID IN (@AwayPitcherID, @HomePitcherID) AND
	PlayByPlay.Inning = 1
GROUP BY
	Pitcher.PlayerID,
	Pitcher.FullName,
	Game.GameDate,
	Game.GameID
ORDER BY Game.GameDate

SELECT
	#Pitcher1stInning.PlayerID,
	#Pitcher1stInning.FullName,
	GameCount = COUNT(*),
	ScoredCount = SUM(#Pitcher1stInning.Scored),
	ScoredPct = ROUND(CAST(SUM(#Pitcher1stInning.Scored) AS FLOAT) / CAST(COUNT(*) AS FLOAT), 2)
FROM #Pitcher1stInning
GROUP BY
	#Pitcher1stInning.PlayerID,
	#Pitcher1stInning.FullName

DROP TABLE #Team1stInning
DROP TABLE #Pitcher1stInning


GO



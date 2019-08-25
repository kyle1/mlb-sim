USE [baseball]
GO

/****** Object:  StoredProcedure [dbo].[pr_GetTeam1stInningData]    Script Date: 8/25/2019 9:34:03 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[pr_GetTeam1stInningData]
	@AwayTeamID		INT,
	@HomeTeamID		INT
AS


SELECT
	AwayAndHome.*
INTO #Team1stInning
FROM (
	SELECT
		Team.TeamID,
		Team.TeamName,
		Game.GameDate,
		Game.GameID,
		Scored =
			CASE
				WHEN SUM(CASE WHEN IsScoringPlay = 1 THEN 1 ELSE 0 END) > 0
				THEN 1
				ELSE 0
			END
	FROM Team
	JOIN Game ON Team.TeamID = Game.AwayTeamID
	JOIN PlayByPlay ON Game.GameID = PlayByPlay.GameID
	WHERE
		Team.TeamID IN (@AwayTeamID, @HomeTeamID) AND
		PlayByPlay.HalfInning = 'top' AND
		PlayByPlay.Inning = 1
	GROUP BY
		Team.TeamID,
		Team.TeamName,
		Game.GameDate,
		Game.GameID

	UNION ALL

	SELECT
		Team.TeamID,
		Team.TeamName,
		Game.GameDate,
		Game.GameID,
		Scored =
			CASE
				WHEN SUM(CASE WHEN IsScoringPlay = 1 THEN 1 ELSE 0 END) > 0
				THEN 1
				ELSE 0
			END
	FROM Team
	JOIN Game ON Team.TeamID = Game.HomeTeamID
	JOIN PlayByPlay ON Game.GameID = PlayByPlay.GameID
	WHERE
		Team.TeamID IN (@AwayTeamID, @HomeTeamID) AND
		PlayByPlay.HalfInning = 'bottom' AND
		PlayByPlay.Inning = 1
	GROUP BY
		Team.TeamID,
		Team.TeamName,
		Game.GameDate,
		Game.GameID
	) AwayAndHome

SELECT
	#Team1stInning.TeamID,
	#Team1stInning.TeamName,
	GameCount = COUNT(*),
	ScoredCount = SUM(#Team1stInning.Scored),
	ScoredPct = ROUND(CAST(SUM(#Team1stInning.Scored) AS FLOAT) / CAST(COUNT(*) AS FLOAT), 2)
FROM #Team1stInning
GROUP BY
	#Team1stInning.TeamID,
	#Team1stInning.TeamName
ORDER BY
	ScoredPct DESC

DROP TABLE #Team1stInning


GO



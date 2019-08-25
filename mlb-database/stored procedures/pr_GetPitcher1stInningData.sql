USE [baseball]
GO

/****** Object:  StoredProcedure [dbo].[pr_GetPitcher1stInningData]    Script Date: 8/25/2019 9:33:16 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[pr_GetPitcher1stInningData] 
	@AwayPitcherID		INT,
	@HomePitcherID		INT
AS


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
INTO #PitcherGames
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
	#PitcherGames.PlayerID,
	#PitcherGames.FullName,
	GameCount = COUNT(*),
	ScoredCount = SUM(#PitcherGames.Scored),
	ScoredPct = ROUND(CAST(SUM(#PitcherGames.Scored) AS FLOAT) / CAST(COUNT(*) AS FLOAT), 2)
FROM #PitcherGames
GROUP BY
	#PitcherGames.PlayerID,
	#PitcherGames.FullName

DROP TABLE #PitcherGames


GO



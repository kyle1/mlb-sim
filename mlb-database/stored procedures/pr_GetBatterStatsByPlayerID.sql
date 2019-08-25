USE [baseball]
GO

/****** Object:  StoredProcedure [dbo].[pr_GetBatterStatsByPlayerID]    Script Date: 8/25/2019 9:30:09 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



CREATE PROCEDURE [dbo].[pr_GetBatterStatsByPlayerID]
    @PlayerID INT  
AS


--Batter stats
SELECT
	Player.PlayerID,
	Batter = Player.FullName,
	Player.BatSide,
	PA = COUNT(*),
	'1B' = SUM(CASE WHEN PlayByPlay.Event = 'Single' THEN 1 ELSE 0 END),
	'2B' = SUM(CASE WHEN PlayByPlay.Event = 'Double' THEN 1 ELSE 0 END),
	'3B' = SUM(CASE WHEN PlayByPlay.Event = 'Triple' THEN 1 ELSE 0 END),
	HR = SUM(CASE WHEN PlayByPlay.Event = 'Home Run' THEN 1 ELSE 0 END),
	BB = SUM(CASE WHEN PlayByPlay.Event IN ('Walk', 'Intent Walk') THEN 1 ELSE 0 END),
	IBB = SUM(CASE WHEN PlayByPlay.Event ='Intent Walk' THEN 1 ELSE 0 END),
	SO = SUM(CASE WHEN PlayByPlay.Event = 'Strikeout' THEN 1 ELSE 0 END),
	HBP = SUM(CASE WHEN PlayByPlay.Event = 'Hit By Pitch' THEN 1 ELSE 0 END),
	SH = SUM(CASE WHEN PlayByPlay.Event = 'Sac Bunt' THEN 1 ELSE 0 END),
	SF = SUM(CASE WHEN PlayByPlay.Event = 'Sac Fly' THEN 1 ELSE 0 END),	
	GDP  = SUM(CASE WHEN PlayByPlay.Event = 'Grounded Into DP' THEN 1 ELSE 0 END)
FROM Player
LEFT JOIN PlayByPlay ON Player.PlayerID = PlayByPlay.BatterID
WHERE
	Player.PlayerID = @PlayerID
GROUP BY
	Player.PlayerID,
	Player.FullName,
	Player.BatSide


--Batter probabilities
SELECT
	Player.PlayerID,
	Batter = Player.FullName,
	Player.BatSide,
	PA = COUNT(*),
	OUT = ROUND(CAST(SUM(CASE WHEN PlayByPlay.Event NOT IN ('Single', 'Double', 'Triple', 'Home Run', 'Walk', 'Intent Walk', 
			'Strikeout', 'Hit By Pitch', 'Sac Bunt', 'Sac Fly') THEN 1 ELSE 0 END) AS FLOAT) / CAST(COUNT(*) AS FLOAT), 2),
	'1B' = ROUND(CAST(SUM(CASE WHEN PlayByPlay.Event = 'Single' THEN 1 ELSE 0 END) AS FLOAT) / CAST(COUNT(*) AS FLOAT), 2),
	'2B' = ROUND(CAST(SUM(CASE WHEN PlayByPlay.Event = 'Double' THEN 1 ELSE 0 END) AS FLOAT) / CAST(COUNT(*) AS FLOAT), 2),
	'3B' = ROUND(CAST(SUM(CASE WHEN PlayByPlay.Event = 'Triple' THEN 1 ELSE 0 END) AS FLOAT) / CAST(COUNT(*) AS FLOAT), 2),
	HR = ROUND(CAST(SUM(CASE WHEN PlayByPlay.Event = 'Home Run' THEN 1 ELSE 0 END) AS FLOAT) / CAST(COUNT(*) AS FLOAT), 2),
	BB = ROUND(CAST(SUM(CASE WHEN PlayByPlay.Event IN ('Walk', 'Intent Walk') THEN 1 ELSE 0 END) AS FLOAT) / CAST(COUNT(*) AS FLOAT), 2),
	IBB = ROUND(CAST(SUM(CASE WHEN PlayByPlay.Event ='Intent Walk' THEN 1 ELSE 0 END) AS FLOAT) / CAST(COUNT(*) AS FLOAT), 2),
	SO = ROUND(CAST(SUM(CASE WHEN PlayByPlay.Event = 'Strikeout' THEN 1 ELSE 0 END) AS FLOAT) / CAST(COUNT(*) AS FLOAT), 2),
	HBP = ROUND(CAST(SUM(CASE WHEN PlayByPlay.Event = 'Hit By Pitch' THEN 1 ELSE 0 END) AS FLOAT) / CAST(COUNT(*) AS FLOAT), 2),
	SH = ROUND(CAST(SUM(CASE WHEN PlayByPlay.Event = 'Sac Bunt' THEN 1 ELSE 0 END) AS FLOAT) / CAST(COUNT(*) AS FLOAT), 2),
	--SF = ROUND(CAST(SUM(CASE WHEN PlayByPlay.Event = 'Sac Fly' THEN 1 ELSE 0 END) AS FLOAT) / CAST(COUNT(*) AS FLOAT), 2),
	'SF+' =
		CASE
			WHEN SUM(CASE WHEN PlayByPlay.MenOnBase != 'Empty' AND PlayByPlay.Outs != 2 THEN 1 ELSE 0 END) = 0 THEN NULL
			ELSE ROUND(CAST(SUM(CASE WHEN PlayByPlay.Event = 'Sac Fly' THEN 1 ELSE 0 END) AS FLOAT) /
					CAST(SUM(CASE WHEN PlayByPlay.MenOnBase != 'Empty' AND PlayByPlay.Outs != 2 THEN 1 ELSE 0 END) AS FLOAT), 2)
		END,
	GDP  = ROUND(CAST(SUM(CASE WHEN PlayByPlay.Event = 'Grounded Into DP' THEN 1 ELSE 0 END) AS FLOAT) / CAST(COUNT(*) AS FLOAT), 2)
FROM Player
LEFT JOIN PlayByPlay ON Player.PlayerID = PlayByPlay.BatterID
WHERE
	Player.PlayerID = @PlayerID
GROUP BY
	Player.PlayerID,
	Player.FullName,
	Player.BatSide


GO



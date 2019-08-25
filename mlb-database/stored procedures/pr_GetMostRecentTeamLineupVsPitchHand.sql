USE [baseball]
GO

/****** Object:  StoredProcedure [dbo].[pr_GetMostRecentTeamLineupVsPitchHand]    Script Date: 8/25/2019 9:32:53 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[pr_GetMostRecentTeamLineupVsPitchHand]
	@TeamID		INT,
    @PitchHand	VARCHAR(1)
AS

DECLARE @GameID AS INT

SELECT TOP 1
	@GameID = Game.GameID
FROM Team
JOIN Player ON Team.TeamID = Player.TeamID
JOIN PlayByPlay ON PlayByPlay.BatterID = Player.PlayerID
JOIN Game ON Game.GameID = PlayByPlay.GameID
WHERE
	Team.TeamID = @TeamID AND
	PlayByPlay.PitchHand = @PitchHand AND
	PlayByPlay.Inning = 1
ORDER BY
	Game.GameDate DESC


SELECT TOP 9
	PlayByPlay.AtBatIndex,
	Player.PlayerID,
	Player.FullName
FROM PlayByPlay
JOIN Player ON PlayByPlay.BatterID = Player.PlayerID
WHERE
	PlayByPlay.GameID = @GameID AND
	Player.TeamID = @TeamID
GROUP BY
	PlayByPlay.AtBatIndex,
	Player.PlayerID,
	Player.FullName
ORDER BY
	PlayByPlay.AtBatIndex
	
GO



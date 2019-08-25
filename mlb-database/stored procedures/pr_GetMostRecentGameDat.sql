USE [baseball]
GO

/****** Object:  StoredProcedure [dbo].[pr_GetMostRecentGameDate]    Script Date: 8/25/2019 9:32:38 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[pr_GetMostRecentGameDate] 
    
AS
    SELECT TOP 1
		--GameDate = CONVERT(DATE, Game.GameDate)
		*
	FROM Game
	ORDER BY Game.GameDate DESC
GO



USE [baseball]
GO

/****** Object:  StoredProcedure [dbo].[pr_GetPlayByPlayColumns]    Script Date: 8/25/2019 9:33:44 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



CREATE PROCEDURE [dbo].[pr_GetPlayByPlayColumns] 
    
AS


SELECT
	PlayByPlay.*
FROM PlayByPlay
WHERE
	1 = 2

GO



Imports System.Configuration
Imports System.Data.Common
Imports System.Data.SqlClient
Imports System.Text

Public Class UnableToReRead
    Inherits ApplicationException

    Public Sub New(ByVal strMessage As String)
        MyBase.New(strMessage)
    End Sub

End Class
Public Class clsSQLData

    Private mblnReread As Boolean = True
    Private mobjTransaction As SqlTransaction = Nothing

#Region "Functions and Subs declared PRIVATE"

    Private mstrConnectionName As String = "Local Connection"

    Private mconSQLConnection As SqlClient.SqlConnection
    Private mblnTrxSuccessful As Boolean = True
    Private mblnIsTransaction As Boolean = False
    Private WithEvents madpData As New SqlClient.SqlDataAdapter


    Private Shared mInstance As clsSQLData
    'Private mintLastID As Integer
    Private Const mcintStartAutoIncrement As Integer = 2000000000
    Private mdtbDataAdapterCollection As DataTable
    Private Const cmstrSelectAll As String = "*"

    Public ReadOnly Property SqlConnection As SqlConnection
        Get
            Return mconSQLConnection
        End Get
    End Property

    Public ReadOnly Property HandledExceptions As List(Of ExceptionMessage) = New List(Of ExceptionMessage)

    Private Sub SetupDataSet(ByRef dstData As DataSet)

        Dim tb As DataTable
        Dim dc As DataColumn
        Dim intPrimaryKeyCounter As Integer

        For Each tb In dstData.Tables
            For Each dc In tb.Columns
                dc.AllowDBNull = True
                dc.ReadOnly = False
            Next

            'B. McDonel added the following code to prevent 
            ' a primary key unhandled exception when the user
            ' inserts more than one record into an autoincrement
            ' primary key

            For intPrimaryKeyCounter = 0 To tb.PrimaryKey.Length - 1
                If tb.PrimaryKey(intPrimaryKeyCounter).AutoIncrement Then
                    tb.Columns(tb.PrimaryKey(intPrimaryKeyCounter).ColumnName).AutoIncrementSeed = mcintStartAutoIncrement
                End If
            Next

        Next
    End Sub

    Private Function fnCheckGISSecurity(ByVal conSQLConnection As SqlClient.SqlConnection,
                           ByVal strMenuItem As String, ByVal strUser As String) As Boolean

        Dim colSQLParams As SqlClient.SqlParameterCollection = (New SqlClient.SqlCommand).Parameters
        Dim intReturnValue As Integer
        Dim blnHasSecurity As Boolean = False
        Dim dstData As New DataSet


        colSQLParams.Clear()
        colSQLParams.AddWithValue("@MenuItemName", strMenuItem)
        colSQLParams.AddWithValue("@UserName", strUser)

        dstData = fnExecuteSPReturnDS(conSQLConnection, "gf_SecurityGetMenuItemName", colSQLParams, True)
        intReturnValue = dstData.Tables(0).Rows.Count
        If intReturnValue > 0 Then
            blnHasSecurity = True
        End If

        Return blnHasSecurity

    End Function

    Private Sub UpdateFaxTable(ByVal conSQLConnection As SqlClient.SqlConnection,
                        ByVal ord_ctrl_int As Integer,
                        ByVal strExportPath As String,
                        ByVal strFaxNumber As String)

        Dim colSQLParams As SqlClient.SqlParameterCollection = (New SqlClient.SqlCommand).Parameters

        colSQLParams.Clear()
        colSQLParams.AddWithValue("@ord_ctrl_int", ord_ctrl_int)
        colSQLParams.AddWithValue("@fax_path", strExportPath)
        colSQLParams.AddWithValue("@fax_number", strFaxNumber)
        fnExecuteSP(conSQLConnection, "pr_gfIV_FaxTableUpdate", colSQLParams)

    End Sub

    Private Function fnBuildStandardSQLAdp(
                ByVal conSQLConnection As SqlClient.SqlConnection,
                ByVal strTableName As String) As SqlClient.SqlDataAdapter

        Dim cmd As New SqlClient.SqlCommand
        cmd.Transaction = mobjTransaction
        Dim adpSQL As SqlClient.SqlDataAdapter
        Dim objSqlCommandBuilder As SqlClient.SqlCommandBuilder
        Dim drwDataAdapter As DataRow
        Dim adpSqlAdapter As New SqlClient.SqlDataAdapter
        Dim dtbPrimaryKey As DataTable
        Dim drwPrimaryKey As DataRow
        Dim strPrimaryKey As String

        If mdtbDataAdapterCollection.Select("strTableName = '" & strTableName & "' AND strColumns = '" & cmstrSelectAll & "'").Length > 0 Then

            adpSQL = CType(mdtbDataAdapterCollection.Select("strTableName = '" & strTableName & "' AND strColumns = '" & cmstrSelectAll & "'")(0)("objSQLAdapter"), SqlClient.SqlDataAdapter)
            adpSQL.MissingSchemaAction = MissingSchemaAction.AddWithKey

        Else

            '
            '
            'mike's rewrite to allow better updating/inserting of multi column primary keys and stored
            'procedures which had an identity column or primary key for a table other than the updated/inserted 
            'table listed before the actual table's primary key.
            '
            '
            cmd.Connection = conSQLConnection
            cmd.Transaction = mobjTransaction
            adpSqlAdapter.SelectCommand = cmd
            'Fill the dataset with the table structure
            'get the primary key(s) for the table
            'putting a comma between column names of the primary key consists of more than 1 column
            cmd.CommandText = "sp_pkeys '" & strTableName & "'"
            dtbPrimaryKey = New DataTable()
            adpSqlAdapter.Fill(dtbPrimaryKey)
            For Each drwPrimaryKey In dtbPrimaryKey.Rows
                If Not IsNothing(strPrimaryKey) Then
                    strPrimaryKey &= "," & fnCheckdbnull(drwPrimaryKey("COLUMN_NAME"))
                Else
                    strPrimaryKey = fnCheckdbnull(drwPrimaryKey("COLUMN_NAME"))
                End If
            Next
            '
            '
            'END OF mike's rewrite to allow better updating/inserting of multi column primary keys and stored
            'procedures which had an identity column or primary key for a table other than the updated/inserted 
            'table listed before the actual table's primary key.
            '
            '

            adpSQL = New SqlClient.SqlDataAdapter(
                    "SELECT * FROM " & strTableName, conSQLConnection)
            adpSQL.MissingSchemaAction = MissingSchemaAction.AddWithKey
            objSqlCommandBuilder = New SqlClient.SqlCommandBuilder(adpSQL)

            AddHandler adpSQL.RowUpdated, AddressOf OnRowUpdated
            AddHandler adpSQL.RowUpdating, AddressOf OnRowUpdating

            drwDataAdapter = mdtbDataAdapterCollection.NewRow

            drwDataAdapter("strTableName") = strTableName
            drwDataAdapter("dtbDataTable") = DBNull.Value
            drwDataAdapter("strColumns") = cmstrSelectAll
            drwDataAdapter("objSQLAdapter") = adpSQL
            '
            '
            'mike's rewrite to allow better updating/inserting of multi column primary keys and stored
            'procedures which had an identity column or primary key for a table other than the updated/inserted 
            'table listed before the actual table's primary key.
            '
            '

            'add the string of primary keys to the data adapter's table
            drwDataAdapter("strPrimaryKey") = strPrimaryKey
            '
            '
            'END OF mike's rewrite to allow better updating/inserting of multi column primary keys and stored
            'procedures which had an identity column or primary key for a table other than the updated/inserted 
            'table listed before the actual table's primary key.
            '
            '

            mdtbDataAdapterCollection.Rows.Add(drwDataAdapter)

        End If

        Return adpSQL

    End Function

    Private Function fnExecuteSP(ByVal conSQLConnection As SqlClient.SqlConnection,
                            ByVal strSPName As String,
                            Optional ByRef colSQLParams As _
                                System.Data.SqlClient.SqlParameterCollection = Nothing) _
                            As Integer

        Dim cmd As New SqlClient.SqlCommand
        cmd.Transaction = mobjTransaction
        Dim intReturnValue As Integer = -9999
        Dim arcolSQLParams(99) As SqlClient.SqlParameter
        Dim SQLParameter As SqlClient.SqlParameter

        cmd.Connection = conSQLConnection
        cmd.CommandText = strSPName
        cmd.CommandTimeout = 500
        cmd.CommandType = CommandType.StoredProcedure

        If cmd.Connection.State = ConnectionState.Closed Or cmd.Connection.State = ConnectionState.Broken Then
            cmd.Connection.Open()
        End If

        If Not colSQLParams Is Nothing Then
            colSQLParams.CopyTo(arcolSQLParams, 0)
            colSQLParams.Clear()
            For Each SQLParameter In arcolSQLParams
                If Not SQLParameter Is Nothing Then
                    cmd.Parameters.Add(SQLParameter)
                End If
            Next
        End If

        cmd.Parameters.Add("@ReturnValue", SqlDbType.Int)
        cmd.Parameters("@ReturnValue").Direction = ParameterDirection.ReturnValue

        cmd.ExecuteNonQuery()

        colSQLParams = cmd.Parameters
        intReturnValue = CType(Val(fnCheckdbnull(colSQLParams("@ReturnValue").Value)), Integer)

        Return intReturnValue

    End Function

    Public Function fnExecuteCMD(ByVal conSQLConnection As SqlClient.SqlConnection,
                            ByVal strCommandText As String) As Integer

        HandledExceptions.Clear()

        Dim cmd As New SqlClient.SqlCommand(strCommandText, conSQLConnection)
        cmd.Transaction = mobjTransaction

        Dim intReturnValue As Integer = -9999

        cmd.CommandTimeout = 500
        cmd.CommandType = CommandType.Text

        If cmd.Connection.State = ConnectionState.Closed Or
            cmd.Connection.State = ConnectionState.Broken Then
            cmd.Connection.Open()
        End If

        intReturnValue = cmd.ExecuteNonQuery()
        Return intReturnValue

    End Function

    Private Function fnExecuteSPReturnDS(ByVal conSQLConnection As SqlClient.SqlConnection,
                                ByVal strSPName As String,
                                ByRef colSQLParams As SqlClient.SqlParameterCollection,
                                ByVal blnEnforceConstraints As Boolean) As DataSet

        Dim cmd As New SqlClient.SqlCommand(strSPName, conSQLConnection)
        cmd.Transaction = mobjTransaction
        Dim adp As New SqlClient.SqlDataAdapter(cmd)
        Dim dst As New DataSet
        Dim arcolSQLParams(99) As SqlClient.SqlParameter
        Dim SQLParameter As SqlClient.SqlParameter
        Dim dtb As DataTable
        Dim blnError As Boolean = False
        Dim intError As Integer = 0
        Dim intMaxTableCount As Integer = 5

        Try
            'bmcdonel 2018-02-07
            'Added the code to set EnforceConstraints because there are certain stored procedures
            'that have a unique constrained column pulled back with the same value (for example with a left outer join).
            'The code can then choose to pass in whether it wants to enforce constraints or not.  The default
            'value for EnforceConstraints is true.
            If Not blnEnforceConstraints Then
                dst.EnforceConstraints = False
            End If

            If conSQLConnection.State = ConnectionState.Broken Or
                conSQLConnection.State = ConnectionState.Closed Then
                conSQLConnection.Open()
            End If

            adp.MissingSchemaAction = MissingSchemaAction.AddWithKey
            adp.SelectCommand.Connection = conSQLConnection
            adp.SelectCommand.CommandType = CommandType.StoredProcedure
            adp.SelectCommand.CommandTimeout = 500

            If Not colSQLParams Is Nothing Then
                colSQLParams.CopyTo(arcolSQLParams, 0)
                colSQLParams.Clear()
                For Each SQLParameter In arcolSQLParams
                    If Not SQLParameter Is Nothing Then
                        adp.SelectCommand.Parameters.Add(SQLParameter)
                    End If
                Next
            End If

            'mwaits 2011-01-12
            'added error catching for sql server 2005/8 which can cause constraint
            'exceptions when the records returned duplicate the primary key for the table
            'or have 2 nulls for the table (left outer joins for records which don't exist)
            'if it errors, we clear the records and try filling again.  
            'it seems that clearing the rows and refilling will bring back the data correctly
            'on a table by table basis.
            'intMaxTableCount is set in this module
            Do While blnError Or intError < intMaxTableCount

                Try

                    blnError = False
                    adp.Fill(dst)
                    intError = 10

                Catch exc As ConstraintException When intError < intMaxTableCount

                    '2016.02.29 scox
                    'In the process of making a change to Inquiry, I discovered the one of the reasons Inquiry runs so
                    'slow is because it always runs the inventory summary stored procedure twice. No one knew this was
                    'happening because we do not log the exception. This code will attempt to log the exception to the
                    'current connection immediately and add the exception to a public property so after the function 
                    'returns, the developer can look at the logged/handled flags and choose if they wish to act upon the
                    'exception or not.
                    'This means that all public functions will need to clear this property of all exceptions before running.

                    ' 04.15.19 Alex Villarreal
                    ' Added additional code to log the parameters being used when the SP throws the exception
                    ' the information will be useful to the developer to figure what is causing the enable constraints error and
                    ' if the error needs to be corrected.
                    ' Trying to parse the parameters is wrapped on a try catch, that way if it fails, the error will still get logged
                    Dim objParameters As New StringBuilder
                    Dim strParameters As String = String.Empty

                    Try

                        Dim intParameterCount = adp.SelectCommand.Parameters.Count

                        If (adp.SelectCommand.Parameters.Count > 0) Then

                            For Each objParam As SqlParameter In adp.SelectCommand.Parameters
                                intParameterCount -= 1
                                objParameters.Append(objParam.ParameterName).Append(": ").Append(objParam.Value).Append(If(intParameterCount > 0, " | ", String.Empty))
                            Next

                        End If

                        If (objParameters.Length > 0) Then
                            strParameters = objParameters.ToString()
                        End If

                    Catch
                        ' Something went wrong with the parsing of the parameters. Continue logging the error without the parameters
                        strParameters = String.Empty
                    End Try

                    Dim exceptionMessage As New ExceptionMessage()
                    exceptionMessage.Message = exc.GetBaseException().Message
                    exceptionMessage.Exception = exc
                    exceptionMessage.WasHandled = True
                    exceptionMessage.WasLogged = True

                    Try
                        '2016.04.12 scox
                        'Changed to use a different fnLogError call to allow an ErrorProcedure override with the stored procedure name

                        ' 04.15.19 Alex Villarreal
                        ' Added the strParameters variable to the values passed in to the function
                        fnLogError(mstrConnectionName, exc, True, strProcedureName:=strSPName, strParameters:=strParameters)
                    Catch ex As Exception
                        exceptionMessage.WasLogged = False
                    End Try

                    HandledExceptions.Add(exceptionMessage)

                    blnError = True
                    intError += 1

                    For Each dtb In dst.Tables
                        dtb.Rows.Clear()
                        dtb.AcceptChanges()
                    Next

                End Try

            Loop

            SetupDataSet(dst)
            colSQLParams = adp.SelectCommand.Parameters

            Return dst

        Catch exc As DuplicateNameException
            'Return the dataset after the Fill command had an error with duplicate names
            'This was needed to depict schema of views or tables without indices
            'HW: 10/9/2006 - DataDictonary
            Return dst

        Finally


        End Try

    End Function

    Private Function fnExecuteCMDReturnDS(ByVal conSQLConnection As _
                                            SqlClient.SqlConnection,
                                          ByVal strCommandText As String) As DataSet

        Dim cmd As New SqlClient.SqlCommand(strCommandText, conSQLConnection)
        cmd.Transaction = mobjTransaction
        Dim adp As New SqlClient.SqlDataAdapter(cmd)
        Dim dst As New DataSet
        Dim arcolSQLParams(99) As SqlClient.SqlParameter


        If conSQLConnection.State = ConnectionState.Broken Or conSQLConnection.State = ConnectionState.Closed Then
            conSQLConnection.Open()
        End If

        adp.MissingSchemaAction = MissingSchemaAction.AddWithKey
        adp.SelectCommand.Connection = conSQLConnection
        adp.SelectCommand.CommandType = CommandType.Text
        adp.SelectCommand.CommandTimeout = 500

        adp.Fill(dst)

        Return dst

    End Function
    Private Function fnExecuteCMDReturnReader(ByVal conSQLConnection As _
                                            SqlClient.SqlConnection,
                                          ByVal strCommandText As String) As SqlDataReader

        Dim cmd As New SqlClient.SqlCommand(strCommandText, conSQLConnection)
        cmd.Transaction = mobjTransaction
        Dim rdr As SqlDataReader

        If conSQLConnection.State = ConnectionState.Broken Or conSQLConnection.State = ConnectionState.Closed Then
            conSQLConnection.Open()
        End If

        rdr = cmd.ExecuteReader

        Return rdr

    End Function

    Private Sub BeginTransaction(ByVal conSQLConnection As SqlClient.SqlConnection)

        Dim cmd As New SqlClient.SqlCommand

        Try
            cmd.Transaction = mobjTransaction
            cmd.CommandText = "BEGIN TRAN"
            cmd.CommandType = CommandType.Text

            cmd.Connection = conSQLConnection
            cmd.ExecuteNonQuery()

        Catch exc As System.Exception
            mblnTrxSuccessful = False
            Throw

        Finally
            cmd = Nothing

        End Try

    End Sub

    Private Sub CommitTransaction(ByVal conSQLConnection As SqlClient.SqlConnection)

        Dim cmd As New SqlClient.SqlCommand

        Try
            cmd.Transaction = mobjTransaction
            cmd.CommandText = "COMMIT TRAN"
            cmd.CommandType = CommandType.Text

            cmd.Connection = conSQLConnection
            cmd.ExecuteNonQuery()

        Catch exc As System.Exception
            mblnTrxSuccessful = False
            Throw

        Finally
            cmd = Nothing

        End Try

    End Sub

    Private Sub RollBackTransaction(ByVal conSQLConnection As SqlClient.SqlConnection)

        Dim cmd As New SqlClient.SqlCommand

        Try
            cmd.Transaction = mobjTransaction
            cmd.CommandText = "ROLLBACK TRAN"
            cmd.CommandType = CommandType.Text

            cmd.Connection = conSQLConnection
            cmd.ExecuteNonQuery()

        Catch exc As System.Exception
            mblnTrxSuccessful = False
            Throw

        Finally
            cmd = Nothing

        End Try

    End Sub

#End Region

    Public ReadOnly Property ConnectionName() As String
        Get
            Return mstrConnectionName
        End Get
    End Property

    Public ReadOnly Property TrxSuccessful() As Boolean
        Get
            Return mblnTrxSuccessful
        End Get
    End Property

    Public ReadOnly Property IsTransaction() As Boolean
        Get
            Return mblnIsTransaction
        End Get
    End Property

    Private Sub OnRowUpdated(ByVal sender As Object, ByVal e As SqlClient.SqlRowUpdatedEventArgs)

        Dim strColumnName As String = ""
        Dim cmdSqlCommand As New SqlClient.SqlCommand
        cmdSqlCommand.Transaction = mobjTransaction
        Dim tbReRead As New DataTable
        Dim adpReRead As New SqlClient.SqlDataAdapter
        Dim drwReRead As DataRow
        Dim dc As DataColumn
        Dim objObject As Object
        Dim strQuote As String
        Dim strAutoIncrementPrimaryKeyColumn As String = "" 'This could contain zero or one column only
        Dim strPrimaryKeyColumns As String = "" 'This could contain zero, one, or mutiple columns separated by a comma.
        'strPrimaryKeys will only contain every column(s) that is a primary key on the table that is being updated/inserted/deleted.
        ' It will not have columns from other tables and it may contain more primary key columns than are in the e.Row.Table.
        ' For example, if e.Row.Table only has one of the primary key columns, but the table actually has 3 Primary Key columns,
        ' then strPrimaryKeys will contain all 3 Primary Key columns.
        Dim strUniqueColumns As String = "" 'This could contain zero, one, or mutiple columns separated by a comma.
        Dim strWhereClauseColumnsAndValuesOnlyToReReadRecord As String = ""
        Dim blnInMemoryTableHasEveryPrimaryKey As Boolean
        Dim blnIncludeColumnInSelect As Boolean

        'B. McDonel & M. Waits 2013/02/01
        ' Revamped the logic within this sub routine.  This procedure
        ' should now properly handle the following situations:
        ' multi-column primary keys
        ' no primary keys, but there is at least one unique column to identify the record
        ' And if the record is unable to be re-read from the database, then the mblnReread
        ' flag is set to false because it cannot be assumed that this record has the same
        ' data that is in the database.

        'mblnReread should not be set to true

        'The hierarchy of the columns to build the SQL Statement is:
        ' strAutoIncrementPrimaryKeyColumn - 1st Highest
        ' strPrimaryKeyColumns - 2nd Highest
        ' strUniqueColumns - 3rd Highest

        If e.StatementType = StatementType.Insert _
                Or e.StatementType = StatementType.Update Then

            'get the primary keys for the table
            strPrimaryKeyColumns = fnCheckdbnull(mdtbDataAdapterCollection.Select("strTableName = '" & e.Row.Table.TableName & "' AND strColumns <> '" & cmstrSelectAll & "'")(0)("strPrimaryKey"))

            'Now find out if every primary key column is in the table that was just updated.
            ' Initialize blnInMemoryTableHasEveryPrimaryKey to whether or not the
            ' length of strPrimaryKeyColumns is greater than zero because if the
            ' length is zero, then the loop will not set it to false.
            blnInMemoryTableHasEveryPrimaryKey = (strPrimaryKeyColumns.Trim.Length > 0)

            For Each strIdColumnName In strPrimaryKeyColumns.Split(","c)
                'When strPrimaryKeyColumns is a blank string
                ' then this loop iterates once with a blank string
                ' column name.

                strIdColumnName = strIdColumnName.Trim

                If strIdColumnName <> "" Then

                    If Not e.Row.Table.Columns.Contains(strIdColumnName) Then
                        'If the primary key column does not exist in the 
                        ' e.Ray.Table, then the table in memory 

                        blnInMemoryTableHasEveryPrimaryKey = False


                    End If
                End If

            Next

            'strWhereClauseColumnsAndValuesOnlyToReReadRecord is used to build the WHERE part of the SQL Statement 
            ' to re-read the record that is being inserted/updated, so initialize it to a blank string
            strWhereClauseColumnsAndValuesOnlyToReReadRecord = ""

            'was a primary key provided?  Epicor doesn't have primary keys
            If strPrimaryKeyColumns.Trim.Length = 0 Then
                'if there are no primary keys, loop through the columns looking for a or unique columns

                strAutoIncrementPrimaryKeyColumn = "" 'Because there is not at least one primary key column, then there is not an Auto Increment Primary Key column

                'But there could be at least one unique column.

                For Each dc In e.Row.Table.Columns
                    If dc.Unique Then

                        If strUniqueColumns.Length <> 0 Then
                            strUniqueColumns &= ","
                        End If

                        strUniqueColumns &= dc.ColumnName

                    End If
                Next

                'This was left in because it is used and currently we do not have a situation that this would throw an exception
                ' but the following code would throw an exception in the following situation:
                ' Table1 is being updated, but the in-memory table (e.Row.Table) was populated with a stored procedure
                ' that selected from Table2 and Table1 does not have any primary keys and Table2.UniqueColumn was selected
                ' and exists in e.Row.Table.
                ' That example would think that Table2.UniqueColumn could be used to generate the where part of the query,
                ' but when this query runs, it will throw an exception because UniqueColumn does not exist for Table1.
                ' The quickest and easiest way to resolve this would be populate mdtbDataAdapterCollection with another
                ' column: strEveryColumn.  This new column would hold every column that exists for the table being
                ' inserted/updated/deleted and then the code above that sets strUniqueColumns could use strEveryColumn
                ' to make sure that the unique column exists for the table being inserted/updated and if it does, then
                ' it can added to strUniqueColumns, other wise it will be ignored and most likely this case statement
                ' will pass to the next Case statement (to generate the WHERE clause using indexes).

            Else
                'Determine if any of the primary keys are identity columns

                For Each strColumnName In strPrimaryKeyColumns.Split(","c)
                    If strColumnName.Trim <> "" Then
                        If e.Row.Table.Columns.Contains(strColumnName) Then
                            If e.Row.Table.Columns(strColumnName).AutoIncrement Then
                                strAutoIncrementPrimaryKeyColumn = strColumnName
                            End If
                        End If
                    End If
                Next

            End If

            'was an identity value returned?
            If e.Command.Parameters.Contains("@ReturnId") Then
                objObject = e.Command.Parameters("@ReturnId").Value
                'objObject will contain DBNull.Value if the table
                ' did not have an identity column or if it were an update.  
                ' This means that it will not be nothing even if it DBNull.Value.
            End If

            Select Case True

                Case e.StatementType = StatementType.Insert And strAutoIncrementPrimaryKeyColumn.Trim <> "" And fnCheckdbnull(objObject) <> ""
                    'The statement is an insert and there is an autoincrement column and the value in objObject is not a blank string
                    ' The auto increment column must be an integer so quotes are not needed.

                    strWhereClauseColumnsAndValuesOnlyToReReadRecord = strAutoIncrementPrimaryKeyColumn & " = " & objObject.ToString

                Case e.StatementType = StatementType.Update And strAutoIncrementPrimaryKeyColumn.Trim <> ""
                    'There is an auto increment column.
                    ' The auto increment column must be an integer so quotes are not needed.

                    strWhereClauseColumnsAndValuesOnlyToReReadRecord = strAutoIncrementPrimaryKeyColumn & " = " & e.Row(strAutoIncrementPrimaryKeyColumn).ToString

                Case strPrimaryKeyColumns.Trim <> "" And blnInMemoryTableHasEveryPrimaryKey
                    'This could be an insert or an update and there is not an auto increment column, but there is at least one Primary Key Column, so build
                    ' the SQL Statement iterating through each primary key.

                    For Each strIdColumnName In strPrimaryKeyColumns.Split(","c)

                        strIdColumnName = strIdColumnName.Trim

                        If strIdColumnName <> "" Then

                            If e.Row.Table.Columns.Contains(strIdColumnName) Then
                                'The Contains if test line of code is necessary because the e.Row.Table
                                ' may not contain every Primary Key column from that table and strPrimaryKeyColumns
                                ' contains every Primary Key from the table.

                                blnIncludeColumnInSelect = True

                                'does the column need ' around the value?
                                Select Case e.Row(strIdColumnName).GetType.Name.ToUpper()
                                    Case "DBNULL", "CURSOR", "HIERARCHYID", "SQL_VARIANT", "TABLE", "TIMESTAMP", "UNIQUEIDENTIFIER", "XML", "BINARY", "IMAGE", "VARBINARY"
                                        'The timestamp type is reporting as DBNULL in the GetType.Name field.

                                        blnIncludeColumnInSelect = False

                                    Case "STRING", "CHAR", "DATETIME", "DATE", "TIME", "DATETIMEOFFSET", "DATETIME2", "SMALLDATETIME", "TEXT", "NCHAR", "NTEXT", "NVARCHAR"
                                        strQuote = "'"

                                    Case Else
                                        strQuote = ""

                                End Select

                                If blnIncludeColumnInSelect Then

                                    If strWhereClauseColumnsAndValuesOnlyToReReadRecord.Trim.Length <> 0 Then
                                        strWhereClauseColumnsAndValuesOnlyToReReadRecord &= " AND "
                                    End If

                                    'build the column's part of the where clause and add an AND to the end
                                    strWhereClauseColumnsAndValuesOnlyToReReadRecord &= strIdColumnName & " = " & strQuote & e.Row(strIdColumnName).ToString & strQuote

                                End If

                            End If

                        End If

                    Next

                Case strUniqueColumns.Trim <> ""
                    'This could be an insert or an update and there is not an auto increment column or a primary key column, 
                    ' but there is at least one Unique Column, so build the SQL Statement iterating through each unique column.

                    'This was left in because it is used and currently we do not have a situation that this would throw an exception
                    ' but the following code would throw an exception in the following situation:
                    ' Table1 is being updated, but the in-memory table (e.Row.Table) was populated with a stored procedure
                    ' that selected from Table2 and Table1 does not have any primary keys and Table2.UniqueColumn was selected
                    ' and exists in e.Row.Table.
                    ' That example would think that Table2.UniqueColumn could be used to generate the where part of the query,
                    ' but when this query runs, it will throw an exception because UniqueColumn does not exist for Table1.
                    ' The quickest and easiest way to resolve this would be populate mdtbDataAdapterCollection with another
                    ' column: strEveryColumn.  This new column would hold every column that exists for the table being
                    ' inserted/updated/deleted and then the code above that sets strUniqueColumns could use strEveryColumn
                    ' to make sure that the unique column exists for the table being inserted/updated and if it does, then
                    ' it can added to strUniqueColumns, other wise it will be ignored and most likely this case statement
                    ' will pass to the next Case statement (to generate the WHERE clause using indexes).

                    For Each strIdColumnName In strUniqueColumns.Split(","c)

                        strIdColumnName = strIdColumnName.Trim

                        If strIdColumnName <> "" Then

                            blnIncludeColumnInSelect = True

                            'does the column need ' around the value?
                            Select Case e.Row(strIdColumnName).GetType.Name.ToUpper()
                                Case "DBNULL", "CURSOR", "HIERARCHYID", "SQL_VARIANT", "TABLE", "TIMESTAMP", "UNIQUEIDENTIFIER", "XML", "BINARY", "IMAGE", "VARBINARY"
                                    'The timestamp type is reporting as DBNULL in the GetType.Name field.

                                    blnIncludeColumnInSelect = False

                                Case "STRING", "CHAR", "DATETIME", "DATE", "TIME", "DATETIMEOFFSET", "DATETIME2", "SMALLDATETIME", "TEXT", "NCHAR", "NTEXT", "NVARCHAR"
                                    strQuote = "'"

                                Case Else
                                    strQuote = ""

                            End Select

                            If blnIncludeColumnInSelect Then

                                If strWhereClauseColumnsAndValuesOnlyToReReadRecord.Trim.Length <> 0 Then
                                    strWhereClauseColumnsAndValuesOnlyToReReadRecord &= " AND "
                                End If

                                'build the column's part of the where clause
                                strWhereClauseColumnsAndValuesOnlyToReReadRecord &= strIdColumnName & " = " & strQuote & e.Row(strIdColumnName).ToString & strQuote

                            End If

                        End If

                    Next

                Case Else
                    'There is not a primary key on the table and the table does not have a unique column.
                    ' Therefore, we know that the library was able to insert/update the record, which 
                    ' requires some sort of uniqueness on the record.  And at this point, it has
                    ' to be a unique index on at least one column.

                    Dim strDatabaseChangeNameBeforeIndexSelect As String = ""
                    Dim strDatabaseChangeNameToRunIndexSelect As String = ""
                    Dim strSelectIndexInfo As String = ""
                    Dim strTableNameToGetIndexInfo As String = ""
                    Dim intPeriodCounter As Integer = 0
                    Dim intNumberOfPeriods As Integer = 0
                    Dim tbUniqueIndexes As New DataTable
                    Dim tbIndexColumnInfo As New DataTable
                    Dim blnChangeDatabaseToGetIndexInfo As Boolean = False

                    'We need to find the number of periods in the table name to determine
                    ' if the table is one of the following formats:
                    ' 0 periods: tablename
                    ' 1 period:  owner.tablename
                    ' 2 periods: dbname.owner.tablename
                    ' 3 periods: server.dbname.owner.tablename

                    intNumberOfPeriods = 0

                    For intPeriodCounter = 0 To e.Row.Table.TableName.Length - 1

                        If e.Row.Table.TableName(intPeriodCounter) = "." Then

                            intNumberOfPeriods += 1

                        End If

                    Next

                    Select Case intNumberOfPeriods

                        Case 0
                            'No periods means that we just have the table name without the owner and database.
                            'Ex: oehdr, arinpchg_all

                            strTableNameToGetIndexInfo = e.Row.Table.TableName
                            strDatabaseChangeNameBeforeIndexSelect = e.Command.Connection.Database.Replace("[", "").Replace("]", "")
                            strDatabaseChangeNameToRunIndexSelect = e.Command.Connection.Database.Replace("[", "").Replace("]", "")

                            blnChangeDatabaseToGetIndexInfo = False

                        Case 1
                            '1 period means that we have owner.tablename.
                            'Ex: dbo.oehdr, dbo.arinpchg_all

                            strTableNameToGetIndexInfo = e.Row.Table.TableName.Substring(e.Row.Table.TableName.LastIndexOf(".") + 1)
                            strDatabaseChangeNameBeforeIndexSelect = e.Command.Connection.Database.Replace("[", "").Replace("]", "")
                            strDatabaseChangeNameToRunIndexSelect = e.Command.Connection.Database.Replace("[", "").Replace("]", "")

                            blnChangeDatabaseToGetIndexInfo = False

                        Case 2
                            '2 periods means that we have database.owner.tablename.
                            'Ex: GIS.dbo.oehdr, GE01GE.dbo.arinpchg_all

                            strDatabaseChangeNameBeforeIndexSelect = e.Command.Connection.Database.Replace("[", "").Replace("]", "")
                            strDatabaseChangeNameToRunIndexSelect = e.Row.Table.TableName.Substring(0, e.Row.Table.TableName.IndexOf(".")).Replace("[", "").Replace("]", "")
                            strTableNameToGetIndexInfo = e.Row.Table.TableName.Substring(e.Row.Table.TableName.LastIndexOf(".") + 1)

                            If strDatabaseChangeNameBeforeIndexSelect <> strDatabaseChangeNameToRunIndexSelect Then

                                blnChangeDatabaseToGetIndexInfo = True

                            End If

                        Case 3
                            '3 periods means that we have server.database.owner.tablename.
                            'Ex: mis-gisdb.GIS.dbo.oehdr, mis-epicordb.GE01GE.dbo.arinpchg_all

                            strDatabaseChangeNameBeforeIndexSelect = e.Command.Connection.Database.Replace("[", "").Replace("]", "")
                            strDatabaseChangeNameToRunIndexSelect = e.Row.Table.TableName.Substring(e.Row.Table.TableName.IndexOf(".") + 1, (e.Row.Table.TableName.IndexOf(".", e.Row.Table.TableName.IndexOf(".") + 1)) - (e.Row.Table.TableName.IndexOf(".") + 1)).Replace("[", "").Replace("]", "")
                            strTableNameToGetIndexInfo = e.Row.Table.TableName.Substring(e.Row.Table.TableName.LastIndexOf(".") + 1)

                            If strDatabaseChangeNameBeforeIndexSelect <> strDatabaseChangeNameToRunIndexSelect Then

                                blnChangeDatabaseToGetIndexInfo = True

                            End If

                        Case Else
                            'There were more than 3 periods and we do not know how to handle this situation,
                            ' so just run the code below assuming zero periods and if there is an exception
                            ' thrown, it will bubble up.

                            strTableNameToGetIndexInfo = e.Row.Table.TableName
                            strDatabaseChangeNameBeforeIndexSelect = e.Command.Connection.Database.Replace("[", "").Replace("]", "")
                            strDatabaseChangeNameToRunIndexSelect = e.Command.Connection.Database.Replace("[", "").Replace("]", "")

                            blnChangeDatabaseToGetIndexInfo = False

                    End Select

                    If blnChangeDatabaseToGetIndexInfo Then

                        e.Command.Connection.ChangeDatabase(strDatabaseChangeNameToRunIndexSelect)

                    End If

                    'Run the query to get all of the unique indexes on the table.

                    strSelectIndexInfo = "SELECT sysindexes.name " _
                            & "FROM sysindexes " _
                            & "INNER JOIN sysobjects ON sysobjects.id = sysindexes.id " _
                            & "WHERE INDEXPROPERTY(sysindexes.id , sysindexes.name , 'IsUnique') = 1 " _
                            & "    AND sysobjects.name = '" & strTableNameToGetIndexInfo & "'"

                    'The query above to select the indexes for a given table works on SQL Server 2000, 2005, and 2008.
                    '2005 and greater has a better way of selecting the indexes, but the above works and is necessary
                    'for 2000 because the syntax below does not work on SQL Server 2000. The 2005 and greater way would be:
                    'strSelectIndexInfo = "SELECT sys.indexes.name " _
                    '        & "FROM sys.indexes " _
                    '        & "INNER JOIN sys.objects on sys.objects.object_id = sys.indexes.object_id " _
                    '        & "WHERE is_unique = 1 and sys.objects.name = '" & strTableNameToGetIndexInfo & "'"

                    cmdSqlCommand.CommandText = strSelectIndexInfo
                    cmdSqlCommand.Connection = e.Command.Connection
                    cmdSqlCommand.Transaction = mobjTransaction
                    adpReRead.SelectCommand = cmdSqlCommand
                    adpReRead.Fill(tbUniqueIndexes)

                    If tbUniqueIndexes IsNot Nothing AndAlso tbUniqueIndexes.Rows.Count > 0 Then
                        'There should be at least one index that specifies unique columns in the table that was just inserted/updated.
                        ' So at this point, we need to run sp_helpindex on this table to get all of the indexes and then 
                        ' iterate through each index and see if we have all of those columns and if we
                        ' do then use those columns to build a re-read select.  If we do not have all of the columns from any index
                        ' in tbIndexColumnInfo, then we are unable to re-read the record.

                        Dim drwIndex As DataRow
                        Dim intIndexCounter As Integer = 0
                        Dim blnInMemoryTableHasEveryColumn As Boolean = False

                        strSelectIndexInfo = "sp_helpindex '" & strTableNameToGetIndexInfo & "'"

                        cmdSqlCommand.CommandText = strSelectIndexInfo
                        cmdSqlCommand.Connection = e.Command.Connection
                        cmdSqlCommand.Transaction = mobjTransaction
                        adpReRead.SelectCommand = cmdSqlCommand
                        adpReRead.Fill(tbIndexColumnInfo)

                        'tbUniqueIndexes contains the names of the unique indexes
                        'tbIndexColumnInfo contains the names of the indexes and the columns of the index

                        blnInMemoryTableHasEveryColumn = False
                        intIndexCounter = 0

                        While intIndexCounter < tbUniqueIndexes.Rows.Count And Not blnInMemoryTableHasEveryColumn

                            If tbIndexColumnInfo.Select("index_name = '" & fnCheckdbnull(tbUniqueIndexes.Rows(intIndexCounter)("name")) & "'").Length > 0 Then

                                Dim strAllColumns As String = ""
                                Dim intColumnCount As Integer = 0
                                Dim intColumnsFound As Integer = 0

                                drwIndex = tbIndexColumnInfo.Select("index_name = '" & fnCheckdbnull(tbUniqueIndexes.Rows(intIndexCounter)("name")) & "'")(0)

                                'The index_keys can contain the column_name with a (-) at the end that represents that the index is descending (versus the default of ascending).
                                strAllColumns = fnCheckdbnull(drwIndex("index_keys")).Replace("(-)", "")

                                For Each strColumnName In strAllColumns.Split(","c)

                                    strColumnName = strColumnName.Trim

                                    If strColumnName <> "" Then

                                        intColumnCount += 1

                                        If e.Row.Table.Columns.Contains(strColumnName) Then

                                            intColumnsFound += 1
                                            blnIncludeColumnInSelect = True

                                            'does the column need ' around the value?
                                            Select Case e.Row(strColumnName).GetType.Name.ToUpper()
                                                Case "DBNULL", "CURSOR", "HIERARCHYID", "SQL_VARIANT", "TABLE", "TIMESTAMP", "UNIQUEIDENTIFIER", "XML", "BINARY", "IMAGE", "VARBINARY"
                                                    'The timestamp type is reporting as DBNULL in the GetType.Name field.

                                                    blnIncludeColumnInSelect = False

                                                Case "STRING", "CHAR", "DATETIME", "DATE", "TIME", "DATETIMEOFFSET", "DATETIME2", "SMALLDATETIME", "TEXT", "NCHAR", "NTEXT", "NVARCHAR"
                                                    strQuote = "'"

                                                Case Else
                                                    strQuote = ""

                                            End Select

                                            If blnIncludeColumnInSelect Then

                                                If strWhereClauseColumnsAndValuesOnlyToReReadRecord.Trim.Length <> 0 Then
                                                    strWhereClauseColumnsAndValuesOnlyToReReadRecord &= " AND "
                                                End If

                                                'build the column's part of the where clause
                                                strWhereClauseColumnsAndValuesOnlyToReReadRecord &= strColumnName & " = " & strQuote & e.Row(strColumnName).ToString & strQuote

                                            End If

                                        End If

                                    End If

                                Next

                                If intColumnCount > 0 And intColumnCount = intColumnsFound Then
                                    'If there is at least one column in the index and
                                    ' all of the columns in the index were found in the 
                                    ' in-memory table, then we can build the re-read
                                    ' select statement.

                                    blnInMemoryTableHasEveryColumn = True

                                Else
                                    'This index could not be used to build the re-read select statement

                                    strWhereClauseColumnsAndValuesOnlyToReReadRecord = ""

                                End If

                            End If

                            intIndexCounter += 1

                        End While

                    Else
                        'There were not any unique indexes, so we are unable to re-read this record

                        mblnReread = False

                    End If

                    If blnChangeDatabaseToGetIndexInfo Then

                        e.Command.Connection.ChangeDatabase(strDatabaseChangeNameBeforeIndexSelect)

                    End If

            End Select

            'At this point, either we were able to build a complete column and value part of the sql statement 
            ' that should only select one record back or strWhereClauseColumnsAndValuesOnlyToReReadRecord 
            ' is a blank string, which means we were not able to build the select statement.

            If strWhereClauseColumnsAndValuesOnlyToReReadRecord.Trim <> "" Then

                Try
                    Dim strSelectStatementToReReadRecord As String

                    'Select top 10 so then the maximum number of records that can be returns from this query are 10.
                    ' There was a defect at one point that ran this select and it was built incorrectly, so the select
                    ' pulled back the whole table, which obviously took a long time.
                    strSelectStatementToReReadRecord = "SELECT TOP 10 * FROM " & e.Row.Table.TableName & " WHERE " & strWhereClauseColumnsAndValuesOnlyToReReadRecord.Trim

                    'Build the command and adapter to re-read the row into a table
                    cmdSqlCommand.CommandText = strSelectStatementToReReadRecord
                    cmdSqlCommand.Connection = e.Command.Connection
                    cmdSqlCommand.Transaction = mobjTransaction
                    adpReRead.SelectCommand = cmdSqlCommand
                    adpReRead.Fill(tbReRead)

                    'Each identity should be unique and only return
                    'one row.
                    If tbReRead.Rows.Count = 1 Then
                        drwReRead = tbReRead.Rows(0)

                        'Loop each column in the original table used in the 
                        'program. It may have more or less columns than the re-read
                        'record. This could cause and error so each column must be
                        'checked to see if it exists.
                        For Each dc In e.Row.Table.Columns
                            'update the values from the re-read row to the in
                            'memory row.


                            'Check to ensure that the column in the in memory table
                            'exists in the actual table. If it does then use it to
                            'Update the values of the in memory table thus matching
                            'the fields in memory to the record in the table on the
                            'server. This makes concurrenty errors actual concurrency 
                            'errors and not selfinflicted concurrency.
                            If drwReRead.Table.Columns.Contains(dc.ColumnName) Then
                                dc.ReadOnly = False
                                e.Row(dc.Caption) = drwReRead(dc.Caption)
                            End If

                        Next

                    Else
                        'There were either zero or more than one record returned
                        ' from the select to re-read the record.  Either way,
                        ' the record was not re-read and cannot be assumed
                        ' to have the same values that are in the database.

                        mblnReread = False

                    End If

                Catch ex As SqlException

                    mblnReread = False

                End Try

            Else
                'The SQL Command could not be built to re-read the record, so set the mblnReread flag.
                ' When this code returns to caller, this flag being false will throw an exception.

                mblnReread = False

            End If

        End If

    End Sub

    Private Sub OnRowUpdating(ByVal sender As Object, ByVal e As SqlClient.SqlRowUpdatingEventArgs)

        'If the statement is an insert, notify the generated insert
        'command adding a scope_identity return to the statment and 
        'add an output parameter to read the result later in the
        'on row updated procedure
        If e.StatementType = StatementType.Insert Then
            e.Command.Transaction = mobjTransaction
            e.Command.CommandText = String.Concat(e.Command.CommandText,
                " Set @ReturnID = Scope_Identity()")
            e.Command.Parameters.Add("@ReturnId", SqlDbType.Int)
            e.Command.Parameters("@ReturnId").Direction = ParameterDirection.Output
        End If

    End Sub

    Public Sub Update(ByRef dstData As DataSet)

        HandledExceptions.Clear()
        Update(dstData, "")
    End Sub
    Public Sub Update(ByRef dstData As DataSet, ByVal strModifyUser As String)

        'using the underlying table structure, build a standard dataadapter.
        'update the table by calling the dataadapter update routine and 
        'passing the table in the dataset to the adaper.

        HandledExceptions.Clear()

        Dim tblData As New DataTable
        Dim blnBeganTranInThisScope As Boolean = False

        Try

            mblnReread = True

            mblnTrxSuccessful = False

            If mconSQLConnection.State = ConnectionState.Closed Or
                mconSQLConnection.State = ConnectionState.Broken Then
                mconSQLConnection.Open()
            End If

            If IsNothing(mobjTransaction) Then
                blnBeganTranInThisScope = True
                mobjTransaction = mconSQLConnection.BeginTransaction
            End If

            For Each tblData In dstData.Tables

                If strModifyUser <> "" Then
                    SetModifyUser(tblData, strModifyUser)
                End If

                madpData = fnBuildStandardSQLAdp(mconSQLConnection, tblData)

                Try

                    madpData.Update(tblData)

                Catch excSQLException As System.Data.SqlClient.SqlException

                    If excSQLException.Number = 207 Then
                        '207 is the specific exception when a column is not found.

                        'The intention with this catch is to prevent an unhandled exception from being
                        ' thrown when the schema has had a column removed in the table in the database
                        ' but the stored schema in memory still has the column.

                        DeleteTableFromStoredSchema(tblData.TableName)

                        'Now that the records with the table name have been removed, get the data adapter again

                        madpData = fnBuildStandardSQLAdp(mconSQLConnection, tblData)

                        'Retry the update

                        madpData.Update(tblData)

                        'If the exception is still thrown, then it is not because of the column being removed

                    Else

                        Throw

                    End If

                End Try

            Next

            If blnBeganTranInThisScope Then
                mobjTransaction.Commit()
            End If

            mblnTrxSuccessful = True
            'm.waits 2012-07-09 altered because there were concurrency errors which were not being caught and rolled back.
            'Catch exc As System.data.SqlClient.SqlException
        Catch exc As System.Exception
            mblnTrxSuccessful = False
            If Not mobjTransaction Is Nothing Then
                mobjTransaction.Rollback()
            End If

            blnBeganTranInThisScope = False

            Throw

        Finally
            tblData = Nothing

            If blnBeganTranInThisScope Then
                mobjTransaction = Nothing
            End If

        End Try

        If Not mblnReread Then
            Throw New UnableToReRead("Unable to re-read record refresh data")
        End If

    End Sub
    Public Sub Update(ByRef tblData As DataTable)

        HandledExceptions.Clear()
        Update(tblData, "")
    End Sub
    Public Sub Update(ByRef tblData As DataTable, ByVal strModifyUser As String)

        'using the underlying table structure, build a standard dataadapter.
        'update the table by calling the dataadapter update routine and 
        'passing the table in the dataset to the adaper.

        HandledExceptions.Clear()

        Dim blnBeganTranInThisScope As Boolean = False

        Try
            mblnReread = True

            mblnTrxSuccessful = False

            If mconSQLConnection.State = ConnectionState.Closed Or mconSQLConnection.State = ConnectionState.Broken Then
                mconSQLConnection.Open()
            End If

            If IsNothing(mobjTransaction) Then
                blnBeganTranInThisScope = True
                mobjTransaction = mconSQLConnection.BeginTransaction
            End If

            If strModifyUser <> "" Then
                SetModifyUser(tblData, strModifyUser)
            End If

            'Build the data adapter
            madpData = fnBuildStandardSQLAdp(mconSQLConnection, tblData)

            Try

                madpData.Update(tblData)

            Catch excSQLException As System.Data.SqlClient.SqlException

                If excSQLException.Number = 207 Then
                    '207 is the specific exception when a column is not found.

                    'The intention with this catch is to prevent an unhandled exception from being
                    ' thrown when the schema has had a column removed in the table in the database
                    ' but the stored schema in memory still has the column.

                    DeleteTableFromStoredSchema(tblData.TableName)

                    'Now that the records with the table name have been removed, get the data adapter again

                    madpData = fnBuildStandardSQLAdp(mconSQLConnection, tblData)

                    'Retry the update

                    madpData.Update(tblData)

                    'If the exception is still thrown, then it is not because of the column being removed

                Else

                    Throw

                End If

            End Try

            If blnBeganTranInThisScope Then
                mobjTransaction.Commit()
            End If

            mblnTrxSuccessful = True

        Catch exc As System.Exception

            'On an error Rollback the transaction and throw the exception to the 
            'front end to message the user.

            If Not mobjTransaction Is Nothing Then
                mobjTransaction.Rollback()
            End If

            blnBeganTranInThisScope = False
            mblnTrxSuccessful = False

            Throw

        Finally

            If blnBeganTranInThisScope Then
                'Clean up the transaction object
                mobjTransaction = Nothing
            End If

        End Try

        If Not mblnReread Then
            Throw New UnableToReRead("Unable to re-read record refresh data")
        End If

    End Sub

    Private Sub DeleteTableFromStoredSchema(ByVal strTableName As String)

        Dim intSchemaCount As Integer
        Dim drwSchema As DataRow

        'Remove the records with this table name from the stored schemas to force
        ' the library to get the schema again.  It is necessary to interate through
        ' mdtbDataAdapterCollection because one table could be represented multiple
        ' times.  For example, client code could call two different updates to the
        ' same table with different columns (one update is to column1 and column3
        ' of tableTEST and there is another update to column88 and column89 to 
        ' tableTEST; thus, tableTEST would have 2 records in 
        ' mdtbDataAdapterCollection).

        intSchemaCount = 0

        For intSchemaCount = mdtbDataAdapterCollection.Rows.Count - 1 To 0 Step -1

            drwSchema = mdtbDataAdapterCollection.Rows(intSchemaCount)

            If fnCheckdbnull(drwSchema("strTableName")) = strTableName Then

                drwSchema.Delete()

            End If

        Next

    End Sub

    Public Function fnRunSPReturnInt(ByVal strSPName As String,
                ByRef colSqlParams As SqlClient.SqlParameterCollection) As Integer

        Dim intValue As Integer = 0

        HandledExceptions.Clear()

        mblnTrxSuccessful = False
        intValue = fnExecuteSP(mconSQLConnection, strSPName, colSqlParams) 'use modular connection
        mblnTrxSuccessful = True

        Return intValue

    End Function

    Public Function fnRunSPReturnDS(ByVal strSPName As String,
                    ByRef colSqlParams As SqlClient.SqlParameterCollection) As DataSet

        Dim dstData As New DataSet

        HandledExceptions.Clear()

        mblnTrxSuccessful = False
        dstData = fnExecuteSPReturnDS(mconSQLConnection, strSPName, colSqlParams, True) 'use modular connection
        mblnTrxSuccessful = True

        Return dstData

    End Function

    Public Function fnRunSPReturnDS(ByVal strSPName As String,
                                    ByRef colSqlParams As SqlClient.SqlParameterCollection,
                                    ByVal blnEnforceConstraints As Boolean) As DataSet

        Dim dstData As New DataSet

        HandledExceptions.Clear()

        mblnTrxSuccessful = False
        dstData = fnExecuteSPReturnDS(mconSQLConnection, strSPName, colSqlParams, blnEnforceConstraints) 'use modular connection
        mblnTrxSuccessful = True

        Return dstData

    End Function

    Public Function fnRunCmd(ByVal strCommandText As String) As Integer

        Dim intValue As Integer = 0

        HandledExceptions.Clear()

        intValue = fnExecuteCMD(mconSQLConnection, strCommandText)
        Return intValue

    End Function

    Public Function fnRunCmdReturnDS(ByVal strCommandText As String) As DataSet

        Dim dstData As DataSet

        HandledExceptions.Clear()

        dstData = fnExecuteCMDReturnDS(mconSQLConnection, strCommandText)
        SetupDataSet(dstData)
        Return dstData

    End Function

    Public Function fnRunCmdReturnReader(ByVal strCommandText As String) As SqlDataReader

        Dim drdData As SqlDataReader

        HandledExceptions.Clear()

        drdData = fnExecuteCMDReturnReader(mconSQLConnection, strCommandText)
        drdData.Read()
        Return drdData

    End Function

    Public Function fnGetData(ByVal strTableName As String) As DataSet

        Dim adpData As New SqlClient.SqlDataAdapter
        Dim dstData As New DataSet

        HandledExceptions.Clear()

        adpData = fnBuildStandardSQLAdp(mconSQLConnection, strTableName)
        adpData.MissingSchemaAction = MissingSchemaAction.AddWithKey
        adpData.Fill(dstData, strTableName)
        SetupDataSet(dstData)
        Return dstData

    End Function

    Public Function fnGetData(ByVal strSPName As String,
                ByRef colSQLParams As SqlClient.SqlParameterCollection) As DataSet

        Dim dstData As New DataSet

        HandledExceptions.Clear()

        dstData = fnRunSPReturnDS(strSPName, colSQLParams)

        Return dstData

    End Function

    Public Sub FaxTableUpdate(ByVal ord_ctrl_int As Integer,
                              ByVal strExportPath As String,
                              ByVal strFaxNumber As String)

        Dim colSQLParams As SqlClient.SqlParameterCollection = (New SqlClient.SqlCommand).Parameters

        HandledExceptions.Clear()

        colSQLParams.AddWithValue("@ord_ctrl_int", ord_ctrl_int)
        colSQLParams.AddWithValue("@fax_path", strExportPath)
        colSQLParams.AddWithValue("@fax_number", strFaxNumber)
        fnRunSPReturnInt("pr_gfIV_FaxTableUpdate", colSQLParams)

    End Sub

    Public Function fnUserHasGISSecurity(ByVal strMenuItem As String,
        ByVal strUser As String) As Boolean

        Dim blnUserHasGISSecurity As Boolean = False

        HandledExceptions.Clear()


        blnUserHasGISSecurity = fnCheckGISSecurity(mconSQLConnection,
                                strMenuItem, strUser)

        Return blnUserHasGISSecurity


    End Function

    Public Sub BeginTransaction()

        HandledExceptions.Clear()

        If mblnIsTransaction Then
            Try
                mblnTrxSuccessful = True
                If mconSQLConnection.State = ConnectionState.Closed Or
                    mconSQLConnection.State = ConnectionState.Broken Then
                    mconSQLConnection.Open()
                End If

                Call BeginTransaction(mconSQLConnection)

            Catch exc As Exception

                mblnTrxSuccessful = False
                Throw

            End Try
        End If

    End Sub

    Public Sub RollBackTransaction()

        HandledExceptions.Clear()

        If mblnIsTransaction Then
            Try
                If mconSQLConnection.State = ConnectionState.Closed Or
                    mconSQLConnection.State = ConnectionState.Broken Then
                    mconSQLConnection.Open()
                End If

                Call RollBackTransaction(mconSQLConnection)

            Catch exc As Exception

                mblnTrxSuccessful = False
                Throw

            End Try
        End If

    End Sub

    Public Sub CommitTransaction()

        HandledExceptions.Clear()

        If mblnIsTransaction Then
            Try
                If mconSQLConnection.State = ConnectionState.Closed Or
                    mconSQLConnection.State = ConnectionState.Broken Then
                    mconSQLConnection.Open()
                End If

                Call CommitTransaction(mconSQLConnection)

            Catch exc As Exception
                mblnTrxSuccessful = False
                Throw

            Finally


            End Try
        End If

    End Sub

    Public Sub New(ByVal strConnectionName As String, Optional ByVal blnIsTransaction As Boolean = True)

        mstrConnectionName = strConnectionName
        mblnIsTransaction = blnIsTransaction
        mconSQLConnection = New SQLDataConnection(mstrConnectionName).Connection
        mobjTransaction = Nothing
        mblnTrxSuccessful = True

        SetupDataAdapterCollection()

    End Sub

    Public Sub New(ByRef objTransaction As DbTransaction)

        mstrConnectionName = ""
        mblnTrxSuccessful = True
        mblnIsTransaction = True
        mconSQLConnection = CType(objTransaction.Connection, SqlConnection)
        mobjTransaction = CType(objTransaction, SqlTransaction)

        SetupDataAdapterCollection()

    End Sub

    Public Sub New(ByRef objConnection As DbConnection)

        mstrConnectionName = ""
        mblnTrxSuccessful = True
        mblnIsTransaction = True
        mconSQLConnection = CType(objConnection, SqlConnection)
        mobjTransaction = Nothing

        SetupDataAdapterCollection()

    End Sub

    Private Sub SetupDataAdapterCollection()

        'The transaction code has more columns in the datatable
        ' than the clsSQLData code because for an unknown reason, 
        ' the data adapter's sql command properties are set to 
        ' nothing when they are re-grabbed from the 
        ' mdtbDataAdapterCollection.  For this reason, the sql
        ' command properties are stored in the clsSQLTransaction
        ' code.

        mdtbDataAdapterCollection = New DataTable("DataAdapter")

        mdtbDataAdapterCollection.Columns.Add("strTableName", GetType(String), "")
        mdtbDataAdapterCollection.Columns.Add("dtbDataTable", GetType(DataTable), "")
        mdtbDataAdapterCollection.Columns.Add("strColumns", GetType(String), "")
        mdtbDataAdapterCollection.Columns.Add("objSQLAdapter", GetType(SqlClient.SqlDataAdapter), "")
        '
        '
        'mike's rewrite to allow better updating/inserting of multi column primary keys and stored
        'procedures which had an identity column or primary key for a table other than the updated/inserted 
        'table listed before the actual table's primary key.
        '
        '
        'add a primary key column which will hold the primary key column(s) for the table.  Multiple columns
        'will have a comma inserted between the column names
        mdtbDataAdapterCollection.Columns.Add("strPrimaryKey", GetType(String), "")
        '
        '
        'mike's rewrite to allow better updating/inserting of multi column primary keys and stored
        'procedures which had an identity column or primary key for a table other than the updated/inserted 
        'table listed before the actual table's primary key.
        '
        '

    End Sub

    'Build a command to get the full structure of the table.
    'Using the returned structure parse the table to be updated
    'building a select list from the columns in the table to updated
    'which are present in the structure table. Using the limited
    'select list build a standard data adapter for the update.
    'This eliminates the problem of breaking apps by adding 
    'columns to tables.
    Private Function fnBuildStandardSQLAdp(
        ByVal conSQLConnection As SqlClient.SqlConnection,
        ByVal tb As DataTable) As SqlClient.SqlDataAdapter

        Dim adpSqlAdapter As New SqlClient.SqlDataAdapter
        Dim objCmdBuilder As SqlClient.SqlCommandBuilder
        Dim tbFullStructure As New DataTable
        Dim cmd As New SqlClient.SqlCommand
        cmd.Transaction = mobjTransaction
        Dim dc As DataColumn
        Dim strSelect As String = ""
        Dim drwDataAdapter As DataRow
        Dim dtbPrimaryKey As DataTable
        Dim drwPrimaryKey As DataRow
        Dim strPrimaryKey As String

        If mdtbDataAdapterCollection.Select("strTableName = '" & tb.TableName & "' AND strColumns <> '" & cmstrSelectAll & "'").Length > 0 Then
            'The table name already exists in mdtbDataAdapterCollection
            ' which means we already have the full structure
            'The reason strColumns cannot equal cmstrSelectAll is because the overloaded fnBuildStandardSQLAdp does not set
            ' a tbFullStructure datatable, so the object stored in this column for that record is Nothing

            tbFullStructure = CType(mdtbDataAdapterCollection.Select("strTableName = '" & tb.TableName & "' AND strColumns <> '" & cmstrSelectAll & "'")(0)("dtbDataTable"), DataTable)
            strPrimaryKey = CType(mdtbDataAdapterCollection.Select("strTableName = '" & tb.TableName & "' AND strColumns <> '" & cmstrSelectAll & "'")(0)("strPrimaryKey"), String)

        Else
            'This is the first time that the user is attempting to select from this table
            ' which means we need to get the full structure

            'Build a command to get the full structure of the table
            cmd.CommandText = "SELECT * FROM " & tb.TableName & " Where 1=2"
            cmd.Connection = conSQLConnection
            cmd.Transaction = mobjTransaction

            adpSqlAdapter.SelectCommand = cmd
            'Fill the dataset with the table structure
            adpSqlAdapter.Fill(tbFullStructure)

            '
            '
            'mike's rewrite to allow better updating/inserting of multi column primary keys and stored
            'procedures which had an identity column or primary key for a table other than the updated/inserted 
            'table listed before the actual table's primary key.
            '
            '
            'get the primary key(s) for the table
            'putting a comma between column names of the primary key consists of more than 1 column
            cmd.CommandText = "sp_pkeys '" & tb.TableName & "'"
            dtbPrimaryKey = New DataTable()
            adpSqlAdapter.Fill(dtbPrimaryKey)
            For Each drwPrimaryKey In dtbPrimaryKey.Rows
                If Not IsNothing(strPrimaryKey) Then
                    strPrimaryKey &= "," & fnCheckdbnull(drwPrimaryKey("COLUMN_NAME"))
                Else
                    strPrimaryKey = fnCheckdbnull(drwPrimaryKey("COLUMN_NAME"))
                End If
            Next
            '
            '
            'END OF mike's rewrite to allow better updating/inserting of multi column primary keys and stored
            'procedures which had an identity column or primary key for a table other than the updated/inserted 
            'table listed before the actual table's primary key.
            '
            '

        End If

        For Each dc In tbFullStructure.Columns
            'Iterate through each column in the structure table
            'and determine if the column is present in the datatable
            'to be updated. If it is add it to a newly built select
            'list
            If tb.Columns.Contains(dc.ColumnName) Then
                If strSelect.Trim <> "" Then
                    strSelect += ","
                End If
                strSelect += String.Concat(" ", dc.ColumnName)
            End If
        Next

        If mdtbDataAdapterCollection.Select("strTableName = '" & tb.TableName & "' AND strColumns = '" & strSelect & "'").Length > 0 Then
            'This set of columns already has an adapter setup, so use it

            adpSqlAdapter = CType(mdtbDataAdapterCollection.Select("strTableName = '" & tb.TableName & "' AND strColumns = '" & strSelect & "'")(0)("objSQLAdapter"), SqlClient.SqlDataAdapter)
            adpSqlAdapter.MissingSchemaAction = MissingSchemaAction.AddWithKey

            adpSqlAdapter.SelectCommand.Transaction = mobjTransaction
            adpSqlAdapter.UpdateCommand.Transaction = mobjTransaction
            adpSqlAdapter.InsertCommand.Transaction = mobjTransaction
            adpSqlAdapter.DeleteCommand.Transaction = mobjTransaction

        Else
            'This set of columns has not had an adapter setup yet, so it needs to be created

            'Once the columnar select list is built use it to build a standard
            'data adapter.
            adpSqlAdapter = New SqlClient.SqlDataAdapter(
                ("SELECT " & strSelect & " FROM " & tb.TableName & ""),
                conSQLConnection)

            'adpSqlAdapter = New SqlClient.SqlDataAdapter( _
            '   ("SELECT " & strSelect & " FROM " & tb.TableName & " Where 1=2"), _
            '   conSQLConnection)

            adpSqlAdapter.SelectCommand.Transaction = mobjTransaction

            adpSqlAdapter.MissingSchemaAction = MissingSchemaAction.AddWithKey
            objCmdBuilder = New SqlClient.SqlCommandBuilder(adpSqlAdapter)

            adpSqlAdapter.UpdateCommand = objCmdBuilder.GetUpdateCommand
            adpSqlAdapter.UpdateCommand.Transaction = mobjTransaction

            'Get the insert command and add the scope identity to the command
            adpSqlAdapter.InsertCommand = objCmdBuilder.GetInsertCommand
            adpSqlAdapter.InsertCommand.Transaction = mobjTransaction

            adpSqlAdapter.DeleteCommand = objCmdBuilder.GetDeleteCommand
            adpSqlAdapter.DeleteCommand.Transaction = mobjTransaction

            'Add the handler for the row updates.
            AddHandler adpSqlAdapter.RowUpdated, AddressOf OnRowUpdated
            AddHandler adpSqlAdapter.RowUpdating, AddressOf OnRowUpdating

            drwDataAdapter = mdtbDataAdapterCollection.NewRow

            drwDataAdapter("strTableName") = tb.TableName
            drwDataAdapter("dtbDataTable") = tbFullStructure
            drwDataAdapter("strColumns") = strSelect
            drwDataAdapter("objSQLAdapter") = adpSqlAdapter
            '
            '
            'mike's rewrite to allow better updating/inserting of multi column primary keys and stored
            'procedures which had an identity column or primary key for a table other than the updated/inserted 
            'table listed before the actual table's primary key.
            '
            '
            'add the string of primary keys to the data adapter's table
            drwDataAdapter("strPrimaryKey") = strPrimaryKey
            '
            '
            'END OF mike's rewrite to allow better updating/inserting of multi column primary keys and stored
            'procedures which had an identity column or primary key for a table other than the updated/inserted 
            'table listed before the actual table's primary key.
            '
            '

            mdtbDataAdapterCollection.Rows.Add(drwDataAdapter)

        End If

        Return adpSqlAdapter

    End Function

End Class

Friend Class SQLDataConnection

    Private mstrConnectionName As String
    Private mblnIsTransaction As Boolean = False
    Private mconConnection As SqlClient.SqlConnection
    Private MySession As GISSession = GISSession.fnGetInstance

    Friend ReadOnly Property ConnectionName() As String
        Get
            Return mstrConnectionName
        End Get
    End Property

    Friend ReadOnly Property IsTransaction() As Boolean
        Get
            Return mblnIsTransaction
        End Get
    End Property

    Friend ReadOnly Property Connection() As SqlClient.SqlConnection
        Get
            Return mconConnection
        End Get
    End Property

    Friend Sub New(ByVal strConnectionName As String)

        mstrConnectionName = strConnectionName

        If MySession.ConnectionBase.ContainsKey(strConnectionName) Then
            mconConnection = MySession.ConnectionBase.Item(strConnectionName)
        Else
            mconConnection = fnMakeConnection(strConnectionName)
            MySession.ConnectionBase.Add(strConnectionName, mconConnection)
        End If

    End Sub

    Private Function fnGetServer(ByVal strConnectionName As String) As DataRow

        '02.03.16 Alex Villarreal
        'Changed the code to get the connection out of the config file first. This was needed for the applications to run on the outside server.
        'Without this change, the connection would be incorrect. The ConfigurationManager.AppSettings returns null if it is not found.

        Dim conSQL As SqlClient.SqlConnection
        Dim dstServer As New DataSet
        Dim drwServer As DataRow = Nothing
        Dim adpServer As New SqlClient.SqlDataAdapter
        Dim strConnectionString As String
        Dim strServerTableName As String = "AppServer"
        Dim strSecurityPortal As String = ConfigurationManager.AppSettings("SecurityServerName")
        Dim strSecurityDatabaseName As String = ConfigurationManager.AppSettings("SecurityDatabaseName")
        Dim strSecurityUserName As String = ConfigurationManager.AppSettings("SecurityUserName")
        Dim strSecurityPassword As String = ConfigurationManager.AppSettings("SecurityPassword")
        Dim strOPMode As String = "WORKMODEL"
        Dim cmd As New SqlClient.SqlCommand

        If String.IsNullOrEmpty(strSecurityPortal) Then
            strSecurityPortal = "MIS-GISDB"
            strSecurityDatabaseName = "security"
            strSecurityUserName = "security"
            strSecurityPassword = "1sm54c"
        End If

        'Get the operation mode from the connection name.
        'Ex. MPLBetaversion -> BetaVersion
        If Len(strConnectionName) > 3 Then
            strOPMode = strConnectionName.Substring(3, Len(strConnectionName) - 3)
        End If

        'If not working locally (WORKMODEL) then retrieve the
        'Server list from the security Database.
        If strOPMode = "WORKMODEL" Then    ' Check if we are working locally

            'B. McDonel 10/18/2011
            'mstrSecurityPortal changed from "(local)" to the local computer name
            ' with SQL instead of HOST at the end of the computer name.
            ' This has been done because the replacement computers that the 
            ' programmers received do not have a local database installed,
            ' instead there is a virtual machine with a SQL Server.
            strSecurityPortal = Environment.MachineName.Substring(0, Environment.MachineName.Length - 4) & "SQL"

        End If

        strConnectionString = "data source=" & strSecurityPortal & ";" & "initial catalog=" & strSecurityDatabaseName & ";" &
                               "user id=" & strSecurityUserName & ";" & "password =" & strSecurityPassword &
                               ";packet size=4096;persist security info=False"

        conSQL = New SqlClient.SqlConnection(strConnectionString)
        conSQL.Open()

        cmd.Connection = conSQL
        cmd.CommandText = "SELECT AppServerName, AppDBName, AppUserName, AppPassword, AppServerCode" &
                            " FROM AppServer WHERE AppServerCode ='" & strConnectionName & "'"

        adpServer = New SqlClient.SqlDataAdapter(cmd)
        adpServer.MissingSchemaAction = MissingSchemaAction.AddWithKey
        adpServer.Fill(dstServer, strServerTableName)

        If dstServer.Tables.Count > 0 Then
            If dstServer.Tables(0).Rows.Count > 0 Then
                drwServer = dstServer.Tables(0).Rows(0)

                If strConnectionName = "GISWORKMODEL" Then

                    drwServer("AppServerName") = Environment.MachineName.Substring(0, Environment.MachineName.Length - 4) & "SQL"
                    drwServer.Table.AcceptChanges()

                End If

            End If
        End If

        conSQL.Close()
        Return drwServer

    End Function

    'Creating a connection using the connection name
    Private Function fnMakeConnection(ByVal strConnectionName As String) As SqlClient.SqlConnection

        Dim drwServer As DataRow = Nothing
        Dim strDatabaseServerName As String = String.Empty
        Dim strDatabaseName As String = String.Empty
        Dim strDatabaseUserName As String = String.Empty
        Dim strDatabasePassword As String = String.Empty
        Dim strConnectionString As String = String.Empty
        Dim strApplicationName As String = String.Empty

        Dim SC As SQLDataConnection = Nothing

        ' 2016.11.18 Shea Cox
        ' Changed AppDomain.CurrentDomain.FriendlyName to _session.AppName to have more consistency among applications
        ' Refactored the function to be cleaner
        drwServer = fnGetServer(strConnectionName)

        If drwServer Is Nothing Then
            Throw New Exception("Connection " & strConnectionName & " could not be found")
        End If

        strDatabaseServerName = fnCheckdbnull(drwServer("AppServerName"))
        strDatabaseName = fnCheckdbnull(drwServer("AppDBName"))
        strDatabaseUserName = fnCheckdbnull(drwServer("AppUserName"))
        strDatabasePassword = fnCheckdbnull(drwServer("AppPassword"))

        ' ApplicationName(UserName@MachineName[ServerName])
        strApplicationName &= $"{MySession.AppName}({MySession.User}@{MySession.WorkStationName}"
        If Not String.IsNullOrWhiteSpace(MySession.CitrixServerName) Then
            strApplicationName &= $"[{MySession.CitrixServerName}]"
        End If
        strApplicationName &= $")"

        strConnectionString = "data source=" & strDatabaseServerName & ";" &
                              "initial catalog=" & strDatabaseName & ";" &
                              "user id=" & strDatabaseUserName & ";" &
                              "password =" & strDatabasePassword & ";" &
                              "packet size=4096;" &
                              "Application Name = " & strApplicationName & ";" &
                              "persist security info=False"

        Return New SqlConnection(strConnectionString)

    End Function

End Class

Public Class ExceptionMessage
    Public Property Message As String = String.Empty
    Public Property Exception As Exception = Nothing
    Public Property WasLogged As Boolean = False
    Public Property WasHandled As Boolean = False
End Class
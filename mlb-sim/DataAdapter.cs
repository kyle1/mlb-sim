using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mlb_model
{
    class DataAdapter
    {
        //private SqlClient.SqlConnection mSqlConnection = new SqlClient.SqlConnection();

        public static void UpdateDb(string connectionString, string cmdQuery, DataSet ds, string tb)
        {
            SqlConnection connection = new SqlConnection(connectionString);
            SqlCommand cmd = new SqlCommand(cmdQuery, connection);
            //cmd.Transaction = mconSQLConnection.BeginTransaction;
            SqlDataAdapter adp = new SqlDataAdapter(cmd);
            SqlCommandBuilder builder = new SqlCommandBuilder(adp);
            builder.GetInsertCommand();
            adp.Update(ds, tb);
        }

        public static DataSet runSPReturnDS(string connectionString, string storedProcedure, SqlParameterCollection sqlParams)
        {
            SqlConnection connection = new SqlConnection(connectionString);
            SqlCommand cmd = new SqlCommand(storedProcedure, connection);
            //cmd.Transaction = mconSQLConnection.BeginTransaction;
            SqlDataAdapter adp = new SqlDataAdapter(cmd);
            SqlParameter[] asqlParams = new SqlParameter[99];
            SqlParameter SQLParameter = new SqlParameter();
            DataSet ds = new DataSet();
            DataTable dt = new DataTable();
            var isError = false;
            var error = 0;
            var maxTableCount = 5;

            try
            {
                connection.Open();

                adp.MissingSchemaAction = MissingSchemaAction.AddWithKey;
                //adp.SelectCommand.Connection = conSQLConnection;
                adp.SelectCommand.CommandType = CommandType.StoredProcedure;
                adp.SelectCommand.CommandTimeout = 500;


                if (sqlParams != null)
                {
                    sqlParams.CopyTo(asqlParams, 0);
                    sqlParams.Clear();
                    foreach (SqlParameter asqlParam in asqlParams)
                    {
                        if (asqlParam != null)
                        {
                            adp.SelectCommand.Parameters.Add(asqlParam);
                        }
                    }
                }

                while (isError || error < maxTableCount)
                {
                    try
                    {
                        isError = false;
                        adp.Fill(ds);
                        error = 10;
                    }
                    catch (ConstraintException exc) when (error < maxTableCount)
                    {
                        StringBuilder prmBuilder = new StringBuilder();
                        string prm = "";
                        
                        try
                        {
                            var parameterCount = adp.SelectCommand.Parameters.Count;

                            if (adp.SelectCommand.Parameters.Count > 0)
                            {
                                foreach (SqlParameter sqlParam in adp.SelectCommand.Parameters)
                                {
                                    parameterCount += 1;
                                    prmBuilder.Append(sqlParam.ParameterName).Append(": ").Append(sqlParam.Value).Append(parameterCount > 0 ? " | " : "");
                                }
                            }

                            if (prmBuilder.Length > 0)
                            {
                                prm = prmBuilder.ToString();
                            }
                        }
                        catch
                        {
                            //Something went wrong with the parsing of the parameters.
                            //Continue logging the error without the parameters.
                            prm = "";
                        }
                        
                        try
                        {
                            //Log error
                        }
                        catch
                        {
                            //Exception was not logged
                        }

                        isError = true;
                        error += 1;
                        foreach (DataTable dtb in ds.Tables)
                        {
                            dtb.Rows.Clear();
                            dtb.AcceptChanges();
                        }
                    }
                }

                //Setup the DataSet
                foreach (DataTable dtbl in ds.Tables)
                {
                    foreach (DataColumn dc in dt.Columns)
                    {
                        dc.AllowDBNull = true;
                        dc.ReadOnly = false;
                    }
                }

            }
            catch (Exception duplicateNameException)
            {
                return ds;
            }
            return ds;
        }
    }
}

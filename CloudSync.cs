using _36E.waiter;
using _36E_Business___ERP.cloudPush;
using _36E_Business___ERP.helper;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using static _36E_Business___ERP.cloudDB.CloudDB;

namespace _36E_Business___ERP.cloudDB
{
    /*
     * 
     * This class will be used to sync data with the Online Cloud DB.
     * It will also be used to push data to the Right Cloud DB That is Set a Listener for any Data Change.
     * It will communicate with Waiter to Push Data to The Online Cloud DB.
     * It will Communicate with Cloud Push to get data that is being sent from the server.
     * 
     */
    internal static class CloudSync
    {
        // Variables.
        private static string Status { get; set; }
        private static StringBuilder StatusTrace { get; set; }
        private static List<string> pushTokens;
        private static DBConnector dbConn;

        // The Callback related Methods and Variables.
        // Table Name and a dictionary of cloud db instances with an id.
        private static Dictionary<string, Dictionary<string, Select>> selectListerners;

        // Methods.
        internal static void Init(string token)
        {
            try
            {
                // Here we will initialize the required Variables.
                if (selectListerners == null)
                {
                    selectListerners = new Dictionary<string, Dictionary<string, Select>>();
                }
                if (dbConn == null)
                {
                    dbConn = new DBConnector();     // Create a Connection object.
                }
                if (pushTokens == null)
                {
                    pushTokens = new List<string>();
                }
                DBConnector.Init();     // Initialize the DB Connector

                if (pushTokens.Contains(token))
                {
                    // The Token has already been initialized.
                }
                else
                {
                    // The Token has not been initialized.
                    pushTokens.Add(token);
                    // Connect to Cloud Push and Register for Messages.
                    PushManager.Init(token, "cloud_db", (string message) =>
                    {
                        try
                        {
                            // Here we will parse the data and store it into the database.
                            // Validate that the message has not been synced before.
                            JObject msgJObj = JObject.Parse(message);
                            // Get the Data from the database.
                            new Select(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                    .FromSystem("synced_check_")
                                    .AddWhere(new Where("id", Where.Type.EQUAL, msgJObj["sync_id_"]))
                                    .SetOnDataResult((List<Row> rows) =>
                                    {
                                        try
                                        {
                                            // Lets run this on another thread.
                                            try
                                            {
                                                // Here we have got all the rows related to this search.
                                                if (rows.Count > 0)
                                                {
                                                    // We found data that has been synced with this id.
                                                    // This data may have already been synced.
                                                    // Check if it is an update.
                                                    if (msgJObj["type"].ToString().ToUpper() == "UPDATE")
                                                    {
                                                        // Lets check if this specific update has been made previously.
                                                        bool canProceed = true;
                                                        long msgUpdateTime = long.Parse(msgJObj["update_time"].ToString());
                                                        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                                                        {
                                                            // Lets check if this update time exists before.
                                                            if (msgUpdateTime == rows[rowIndex].GetBigInt("update_time_"))
                                                            {
                                                                canProceed = false; // This update has been processed before.
                                                            }
                                                        }

                                                        // The update can proceed.
                                                        if (canProceed)
                                                        {
                                                            // It is an update.
                                                            // Lets proceed with the syncing protocol.
                                                            // We will also send it to the listening Cloud DB Objects.
                                                            // Lets store the data into the local database.
                                                            bool affected = StoreInLocalDB(msgJObj);
                                                            // Send the data to the listening Cloud DB Objects for syncing the UI.
                                                            if (affected)
                                                            {
                                                                // We do not want to send data to the listeners that has not been pushed into the local database.
                                                                // If not done this way, there could be inconsistencies on reload of the APP, etc.
                                                                SendToUpdateListeners(msgJObj);
                                                            }
                                                        }
                                                    }
                                                    else if (msgJObj["type"].ToString().ToUpper() == "DELETE")
                                                    {
                                                        // It is a delete type of sync.
                                                        // Lets Check if we have performed this delete before.
                                                        try
                                                        {
                                                            // Lets Perform this delete with the proper checks.
                                                            bool canProceed = true;
                                                            try
                                                            {
                                                                // Check the Delete.
                                                                long msgDeleteTime = long.Parse(msgJObj["delete_time"].ToString());
                                                                for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                                                                {
                                                                    // Lets check if this delete time exists before.
                                                                    if (msgDeleteTime == rows[rowIndex].GetBigInt("update_time_"))
                                                                    {
                                                                        canProceed = false; // This Delete has been processed before.
                                                                    }
                                                                }
                                                            }
                                                            catch (Exception e)
                                                            {
                                                                // There was an Error.
                                                            }

                                                            // The delete can Proceed.
                                                            if (canProceed)
                                                            {
                                                                // It is an update.
                                                                // Lets proceed with the syncing protocol.
                                                                // We will also send it to the listening Cloud DB Objects.
                                                                // Lets store the data into the local database.
                                                                Row deletedRow = LocalDeleteSyncData(msgJObj);
                                                                // Send the data to the listening Cloud DB Objects for syncing the UI.
                                                                if (deletedRow != null)
                                                                {
                                                                    // We do not want to send data to the listeners that has not been pushed into the local database.
                                                                    // If not done this way, there could be inconsistencies on reload of the APP, etc.
                                                                    SendToDeleteListeners(msgJObj, deletedRow);
                                                                }
                                                            }
                                                        }
                                                        catch (Exception e)
                                                        {
                                                            // There was an Error.
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    // This data has not been synced before.
                                                    // Lets proceed with the syncing protocol.
                                                    // We will also send it to the listening Cloud DB Objects.
                                                    // Lets store the data into the local database.
                                                    if ((msgJObj["type"].ToString().ToUpper() == "WRITE"))
                                                    {
                                                        // It is an insert type of sync.
                                                        bool affected = StoreInLocalDB(msgJObj);
                                                        // Send the data to the listening Cloud DB Objects for syncing the UI.
                                                        if (affected)
                                                        {
                                                            // We do not want to send data to the listeners that has not been pushed into the local database.
                                                            // If not done this way, there could be inconsistencies on reload of the APP, etc.
                                                            SendToInsertListeners(msgJObj);
                                                        }
                                                    }
                                                    else if (msgJObj["type"].ToString().ToUpper() == "UPDATE")
                                                    {
                                                        // Lets check if this specific update has been made previously.
                                                        // It is an update.
                                                        // Lets proceed with the syncing protocol.
                                                        // We will also send it to the listening Cloud DB Objects.
                                                        // Lets store the data into the local database.
                                                        bool affected = StoreInLocalDB(msgJObj);
                                                        // Send the data to the listening Cloud DB Objects for syncing the UI.
                                                        if (affected)
                                                        {
                                                            // We do not want to send data to the listeners that has not been pushed into the local database.
                                                            // If not done this way, there could be inconsistencies on reload of the APP, etc.
                                                            SendToUpdateListeners(msgJObj);
                                                        }
                                                    }
                                                    else if (msgJObj["type"].ToString().ToUpper() == "DELETE")
                                                    {
                                                        // It is a delete type of sync.
                                                        // Lets Check if we have performed this delete before.
                                                        try
                                                        {
                                                            // Lets Perform this delete with the proper checks.
                                                            // It is an update.
                                                            // Lets proceed with the syncing protocol.
                                                            // We will also send it to the listening Cloud DB Objects.
                                                            // Lets store the data into the local database.
                                                            Row deletedRow = LocalDeleteSyncData(msgJObj);
                                                            // Send the data to the listening Cloud DB Objects for syncing the UI.
                                                            if (deletedRow != null)
                                                            {
                                                                // We do not want to send data to the listeners that has not been pushed into the local database.
                                                                // If not done this way, there could be inconsistencies on reload of the APP, etc.
                                                                SendToDeleteListeners(msgJObj, deletedRow);
                                                            }
                                                        }
                                                        catch (Exception e)
                                                        {
                                                            // There was an Error.
                                                        }
                                                    }
                                                }
                                            }
                                            catch (Exception e)
                                            {
                                                // There was an Error.
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            // There was an Error.
                                        }
                                    })
                                    .ExecuteAsync();
                        }
                        catch (Exception e)
                        {
                            // There was an Error.
                        }
                    });
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                AddStatus("Init Err : " + e.Message);
            }
        }

        // Here we store the data in the local database, using the below methods.
        private static bool StoreInLocalDB(JObject msgJObj)
        {
            try
            {
                // Here we will parse the data that has to be stored.
                // We will communicate with the local connections and store into the database.
                switch ((CRUD)Enum.Parse(typeof(CRUD), msgJObj["type"].ToString()))
                {
                    case CRUD.WRITE:
                        // Write in the Local Database.
                        return LocalInsertSyncData(msgJObj);
                    case CRUD.UPDATE:
                        // Update in the Local Database.
                        return LocalUpdateSyncData(msgJObj);
                    case CRUD.DELETE:
                        // Delete from the Local Database.
                        // Will be handled outside this method.
                        return false;
                    default:
                        return false;
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                return false;
            }
        }

        // All the Methods related to syncing data coming from the server to the device will come here.
        // Local Insert will come here.
        private static bool LocalInsertSyncData(JObject msgJObj)
        {
            try
            {
                // Lets Get the data from the Json Object.
                // Lets create the Statement.
                StringBuilder sqlSB = new StringBuilder();
                // Add the Table name.
                string tableName = Regex.Replace(msgJObj["table_name"].ToString(), " ", "");
                // Perform actions according to the table.
                // Validate the table.
                if (Helper.DBValidation.IsSystemTB(tableName) || CloudDB.IsSyncable(tableName))
                {
                    // Create the Statement.
                    sqlSB.Append("INSERT INTO ");
                    // Trying to write into a valid table.
                    sqlSB.Append(tableName);
                    // Get the Columns from the Json Object.
                    JObject colJObj = (JObject)msgJObj["columns"];     // This will contain all the columns. 
                    // Here we will add the required columns of data.
                    // Initialize the String builders to be used for the columns and their values.
                    StringBuilder nameStrB = new StringBuilder();
                    StringBuilder valuePlaceHoldStrB = new StringBuilder();
                    Dictionary<string, object> bindObjs = new Dictionary<string, object>();
                    // Get the Keys of the Columns.
                    IList<string> colKeys = colJObj.Properties().Select(p => p.Name).ToList();
                    for (int i = 0; i < colKeys.Count; i++)
                    {
                        // Get the Data to be inserted from the columns json.
                        // Add the Columns.
                        if (nameStrB.Length > 0 && !(i == colKeys.Count))
                        {
                            nameStrB.Append(", ");
                            valuePlaceHoldStrB.Append(", ");
                        }
                        // Add the Column Name.
                        nameStrB.Append(colKeys[i]);
                        // Add the Placeholders
                        valuePlaceHoldStrB.Append("@")
                            .Append(colKeys[i]);
                        // Add the Objects to be binded.
                        object obj = colJObj[colKeys[i]].ToObject<object>();
                        bindObjs.Add(colKeys[i], obj);
                    }
                    // Lets create the SQL.
                    sqlSB.Append(" ( ")
                        .Append(nameStrB)
                        .Append(" ) VALUES (")
                        .Append(valuePlaceHoldStrB)
                        .Append(" ) ");
                    // Lets create the Command statement.
                    SQLiteCommand dbSess = dbConn.GetCmd();
                    // Lets reset the Session.
                    dbSess.Reset();
                    // Create the Statement.
                    dbSess.CommandText = sqlSB.ToString();
                    // Bind the Params.
                    foreach (string paramName in bindObjs.Keys)
                    {
                        // Bind the Params.
                        SQLiteParameter sqlParam = new SQLiteParameter(paramName, bindObjs[paramName]);
                        // Add the Param to the Command.
                        dbSess.Parameters.Add(sqlParam);
                    }
                    // Lets prepare the statement.
                    dbSess.Prepare();
                    // Lets execute the Statement.
                    int inserted = dbSess.ExecuteNonQuery();
                    dbSess.Dispose();
                    // Now we will insert into the local sync table.
                    try
                    {
                        // Now we will store the data that has to be synced into the system tables to maintain offline syncing.
                        // Store into the Sync Offline Table.
                        // Now lets sync into the Sync Check Table.
                        new Insert(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                            .IntoSystem("synced_check_")
                            .PutColumn(new ColumnData("is_self_written").Put(false))
                            .PutColumn(new ColumnData("crud_type").Put("INSERT"))
                            .PutColumn(new ColumnData("id").Put(msgJObj["sync_id_"]))
                            .PutColumn(new ColumnData("table_name").Put(msgJObj["table_name"]))
                            .PutColumn(new ColumnData("data").Put(msgJObj.ToString()))
                            .SetUpdateTime(long.Parse(colJObj["update_time_"].ToString()))
                            .SetSyncable(false)
                            .Execute();
                    }
                    catch (Exception e)
                    {
                        // There was an Error in the Syncing process.
                    }
                    return inserted > 0;
                }
                else
                {
                    // Not allowed to write in this table.
                    return false;
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                return false;
            }
        }

        // Local Update will come here.
        private static Dictionary<string, object> bindUpdateWhereObjs;
        private static bool LocalUpdateSyncData(JObject msgJObj)
        {
            try
            {
                // Here we will Update the Local database.
                // We will update only one row.
                // Lets create the Statement.
                StringBuilder sqlSB = new StringBuilder();
                // Add the Table Name.
                string tableName = Regex.Replace(msgJObj["table_name"].ToString(), " ", "");
                // Perform actions according to the table authorization.
                if (Helper.DBValidation.IsSystemTB(tableName) || CloudDB.IsSyncable(tableName))
                {
                    // The update is valid.
                    // Create the Statement.
                    sqlSB.Append("UPDATE ");
                    // Add the table name.
                    sqlSB.Append(tableName)
                        .Append(" SET ");

                    // Add the Columns from the JSON Object.
                    JObject colsJObj = (JObject)msgJObj["columns"];
                    Dictionary<string, object> bindObjs = new Dictionary<string, object>();
                    // Get the Keys of the Columns.
                    IList<string> colKeys = colsJObj.Properties().Select(p => p.Name).ToList();
                    for (int i = 0; i < colKeys.Count; i++)
                    {
                        try
                        {
                            // Lets Get the Column Data.
                            string columnName = colKeys[i].Replace(" ", "");
                            JObject colJObj = (JObject)colsJObj[columnName];
                            if (i > 0 && !(i == colKeys.Count))
                            {
                                sqlSB.Append(", ");
                            }
                            switch (colJObj["type"].ToString())
                            {
                                case Update.T.VALUE:
                                    sqlSB.Append(columnName)
                                        .Append(" = @")
                                        .Append(columnName);
                                    bindObjs.Add(columnName, colJObj["value"].ToObject<object>());
                                    break;
                                case Update.T.COLUMN:
                                    sqlSB.Append(columnName)
                                        .Append(" = ")
                                        .Append(colJObj["column"].ToString().Replace(" ", ""));
                                    break;
                                case Update.T.CONCAT:
                                    sqlSB.Append(columnName)
                                        .Append(" = ")
                                        .Append(colJObj["column"].ToString().Replace(" ", ""))
                                        .Append(" || @")
                                        .Append(columnName);
                                    bindObjs.Add(columnName, colJObj["value"].ToObject<object>());
                                    break;
                                case Update.T.OPERATION:
                                    sqlSB.Append(columnName)
                                        .Append(" = ")
                                        .Append(colJObj["column"].ToString().Replace(" ", ""));

                                    switch (colJObj["code"].ToString())
                                    {
                                        case "ADD":
                                            sqlSB.Append(" + ");
                                            break;
                                        case "SUBSCRACT":
                                            sqlSB.Append(" - ");
                                            break;
                                        case "MULTIPLY":
                                            sqlSB.Append(" * ");
                                            break;
                                        case "DIVIDE":
                                            sqlSB.Append(" / ");
                                            break;
                                        case "MODULAR":
                                            sqlSB.Append(" % ");
                                            break;
                                    }
                                    sqlSB.Append(double.Parse(colJObj["value"].ToString().Replace(" ", "")));
                                    break;
                            }
                        }
                        catch (Exception e)
                        {
                            // There was an Error.
                        }
                    }

                    // Add The Where Clause.
                    try
                    {
                        // Add the Where Clause from the JSON Object.
                        bindUpdateWhereObjs = new Dictionary<string, object>();
                        sqlSB.Append(" WHERE sync_id_ = @Wsync_id_ ");
                        bindUpdateWhereObjs.Add("Wsync_id_", msgJObj["sync_id_"]);
                        // Add the other Wheres.
                        JObject whereJObj = (JObject)msgJObj["wheres"];
                        sqlSB.Append(SetupWhereClauses(whereJObj, "", 1, 2));
                    }
                    catch (Exception e)
                    {
                        // There was an Error.
                    }

                    // Lets Create the Statement.
                    SQLiteCommand dbSess = dbConn.GetCmd();
                    // Lets reset the Session.
                    dbSess.Reset();
                    // Create the Statement.
                    dbSess.CommandText = sqlSB.ToString();
                    // Bind the Column Params.
                    foreach (string paramName in bindObjs.Keys)
                    {
                        try
                        {
                            // Bind the Params.
                            SQLiteParameter sqlParam = new SQLiteParameter(paramName, bindObjs[paramName]);
                            // Add the Param to the Command.
                            dbSess.Parameters.Add(sqlParam);
                        }
                        catch (Exception e)
                        {
                            // There was an Error.
                        }
                    }
                    // Bind the Where Params.
                    foreach (string whereName in bindUpdateWhereObjs.Keys)
                    {
                        try
                        {
                            // Bind the Params.
                            SQLiteParameter sqlParam = new SQLiteParameter(whereName, bindUpdateWhereObjs[whereName]);
                            // Add the Param to the Command.
                            dbSess.Parameters.Add(sqlParam);
                        }
                        catch (Exception e)
                        {
                            // There was an Error.
                        }
                    }
                    // Lets Prepare the statement.
                    dbSess.Prepare();
                    // Lets execute the statement.
                    int updated = dbSess.ExecuteNonQuery();
                    dbSess.Dispose();
                    // Now we will insert into the local sync table.
                    try
                    {
                        // Now we will store the data that has to be synced into the system tables to maintain offline syncing.
                        // Store into the Sync Offline Table.
                        // Now lets sync into the Sync Check Table.
                        if (updated > 0)
                        {
                            new Insert(new CloudDB(C.SYSTEM_USER_DB_PASSCODE)).IntoSystem("synced_check_")
                                .PutColumn(new ColumnData("is_self_written").Put(false))
                                .PutColumn(new ColumnData("crud_type").Put("UPDATE"))
                                .PutColumn(new ColumnData("id").Put(msgJObj["sync_id_"]))
                                .PutColumn(new ColumnData("table_name").Put(msgJObj["table_name"]))
                                .PutColumn(new ColumnData("data").Put(msgJObj.ToString()))
                                .SetSyncable(false)
                                .Execute();
                        }
                    }
                    catch (Exception e)
                    {
                        // There was an Error in the Syncing process.
                    }
                    return updated > 0;
                }
                else
                {
                    // Not allowed to write in this table.
                    return false;
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                return false;
            }
        }

        // Local Delete will come here.
        private static Dictionary<string, object> bindDeleteWhereObjs;
        private static Row LocalDeleteSyncData(JObject msgJObj)
        {
            try
            {
                // Here we will Delete the Data from the local database.
                // And send the deleted data as return.
                // Lets get the row from the database.
                // Lets Create the Statement.
                SQLiteCommand dbSess = dbConn.GetCmd();
                bindDeleteWhereObjs = new Dictionary<string, object>();
                // Reset the Session.
                dbSess.Reset();
                // Create the Statement.
                dbSess.CommandText = "SELECT * FROM " + (msgJObj["table_name"].ToString().Replace(" ", ""))
                            + " WHERE sync_id_ = @sync_id_";
                // Bind the Param.
                SQLiteParameter syncParam = new SQLiteParameter("sync_id_", msgJObj["sync_id_"].ToObject<object>());
                // Add the param to the command.
                dbSess.Parameters.Add(syncParam);
                // Lets prepare the statement.
                dbSess.Prepare();
                // Lets Execute the Statement.
                SQLiteDataReader sdr = dbSess.ExecuteReader();
                dbSess.Dispose();

                if (sdr.HasRows)
                {
                    // There is a row with that sync id.
                    Row rowToDelete = CloudDB.SQLiteReaderToRows(sdr)[0];
                    // Create the DB Session
                    SQLiteCommand delSess = dbConn.GetCmd();
                    // Reset the Session.
                    delSess.Reset();
                    // Create the SQL Statement.
                    StringBuilder sqlSB = new StringBuilder("DELETE FROM " + (msgJObj["table_name"].ToString().Replace(" ", "")));
                    // Add The Where Clause.
                    try
                    {
                        // Add the Where Clause from the JSON Object.
                        bindDeleteWhereObjs = new Dictionary<string, object>();
                        sqlSB.Append(" WHERE sync_id_ = @Wsync_id_ ");
                        bindDeleteWhereObjs.Add("Wsync_id_", msgJObj["sync_id_"]);
                        // Add the other Wheres.
                        JObject whereJObj = (JObject)msgJObj["wheres"];
                        sqlSB.Append(SetupWhereClauses(whereJObj, "", 1, 3));

                        // Lets Create a Statement.
                        delSess.CommandText = sqlSB.ToString();
                        // Bind the Params.
                        foreach (string whereName in bindDeleteWhereObjs.Keys)
                        {
                            try
                            {
                                // Bind the Params.
                                SQLiteParameter sqlParam = new SQLiteParameter(whereName, bindDeleteWhereObjs[whereName]);
                                // Add the Param to the Command.
                                delSess.Parameters.Add(sqlParam);
                            }
                            catch (Exception e)
                            {
                                // There was an Error.
                            }
                        }
                        // Lets Prepare the Statement.
                        delSess.Prepare();
                        // Lets Execute the Statement.
                        int deleted = delSess.ExecuteNonQuery();
                        delSess.Dispose();
                        // Now we will insert into the local sync table.
                        try
                        {
                            // Now we will store the data that has to be synced into the system tables to maintain offline syncing.
                            // Store into the Sync Offline Table.
                            // Now lets sync into the Sync Check Table.
                            new Insert(new CloudDB(C.SYSTEM_USER_DB_PASSCODE)).IntoSystem("synced_check_")
                                .PutColumn(new ColumnData("is_self_written").Put(false))
                                .PutColumn(new ColumnData("crud_type").Put("DELETE"))
                                .PutColumn(new ColumnData("id").Put(msgJObj["sync_id_"]))
                                .PutColumn(new ColumnData("table_name").Put(msgJObj["table_name"]))
                                .PutColumn(new ColumnData("data").Put(msgJObj.ToString()))
                                .SetSyncable(false)
                                .Execute();
                        }
                        catch (Exception e)
                        {
                            // There was an Error in the Syncing process.
                        }
                        // Return the Row if the value is deleted.
                        return deleted > 0 ? rowToDelete : null;
                    }
                    catch (Exception e)
                    {
                        // There was an Error.
                        return null;
                    }
                }
                else
                {
                    // There are no rows.
                    return null;
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                return null;
            }
        }

        // Setup the Where Clauses.
        private static string SetupWhereClauses(JObject whereJObj, string whereClause, int type, int RUDType)   // This is Read, Update, Delete Type
        {
            try
            {
                // Lets Create the String
                StringBuilder whereBuild = new StringBuilder(whereClause);
                // Add the Condition
                switch (type)
                {
                    case 1:
                        whereBuild.Append(" AND ");
                        break;
                    case 2:
                        whereBuild.Append(" OR ");
                        break;
                }
                // Add the Main part.
                whereBuild.Append(whereJObj["name"].ToString());
                // Add the Operator.
                switch (whereJObj["condition"].ToString())
                {
                    case "EQUAL":
                        whereBuild.Append(" = ");
                        break;
                    case "GREATER":
                        whereBuild.Append(" > ");
                        break;
                    case "GREATER_EQUAL":
                        whereBuild.Append(" >= ");
                        break;
                    case "LESSER":
                        whereBuild.Append(" < ");
                        break;
                    case "LESSER_EQUAL":
                        whereBuild.Append(" <= ");
                        break;
                    default:
                        return null;
                }
                // Add the Value to be added to the Where clause.
                whereBuild.Append("@W")
                    .Append(whereJObj["name"]);
                if (RUDType == 1)
                {
                    // This is for Select / Read.
                }
                else if (RUDType == 2)
                {
                    // This is for Update
                    bindUpdateWhereObjs.Add("W" + whereJObj["name"].ToString(), whereJObj["data"].ToObject<object>());
                }
                else if (RUDType == 3)
                {
                    // This is for Delete.
                    bindDeleteWhereObjs.Add("W" + whereJObj["name"].ToString(), whereJObj["data"].ToObject<object>());
                }
                if (whereJObj.ContainsKey("and"))
                {
                    // There is more where clauses to add with AND
                    whereBuild = new StringBuilder(SetupWhereClauses((JObject)whereJObj["and"], whereBuild.ToString(), 1, RUDType));
                }
                else if (whereJObj.ContainsKey("or"))
                {
                    // There is more where clauses to add with OR.
                    whereBuild = new StringBuilder(SetupWhereClauses((JObject)whereJObj["or"], whereBuild.ToString(), 2, RUDType));
                }
                return whereBuild.ToString();
            }
            catch (Exception e)
            {
                // There was an Error.
                return "";
            }
        }

        // This method will be used to send the data to the listening objects on an insert into the DB.
        private static void SendToInsertListeners(JObject msgJObj)
        {
            try
            {
                // Here we will parse the data to be sent.
                Row insertedRow = new Row();

                // Lets Get the Data from the Local Database
                bool doProceed = true;
                try
                {
                    // Here we will Get the Data that has been inserted from the local database.
                    // Lets build the SQL to get the Data that has been inserted.
                    StringBuilder sqlBuilder = new StringBuilder();
                    sqlBuilder.Append("SELECT * FROM ")
                        .Append(msgJObj["table_name"].ToString())
                        .Append(" WHERE sync_id_ = @sync_id_");

                    // Lets Get the Data of the Row.
                    // Lets create the Command statement.
                    SQLiteCommand dbSess = dbConn.GetCmd();
                    // Lets reset the Session.
                    dbSess.Reset();
                    // Lets create the Command Text.
                    dbSess.CommandText = sqlBuilder.ToString();
                    // Lets add the params.
                    SQLiteParameter sqlParam = new SQLiteParameter("sync_id_", msgJObj["sync_id_"].ToString());
                    dbSess.Parameters.Add(sqlParam);
                    // Lets Prepare the Statement.
                    dbSess.Prepare();
                    // Lets Execute the Reader Query.
                    insertedRow = SQLiteReaderToRows(dbSess.ExecuteReader())[0];
                    dbSess.Dispose();
                }
                catch (Exception er)
                {
                    // There was an Error.
                    doProceed = false;
                }

                // We will push the data to the listeners.
                if (doProceed)
                {
                    // Get the listeners according to the table
                    if (selectListerners.ContainsKey(msgJObj["table_name"].ToString()))
                    {
                        // There are data change listeners.
                        // Lets setup for cleanup
                        List<string> cleanUpArr = new List<string>();

                        // Check the type of change in data.
                        Dictionary<string, Select> selects = selectListerners[msgJObj["table_name"].ToString()];
                        // Lets now send the data to the select objects.
                        long rlsID = insertedRow.GetBigInt("rls_id_");
                        short rlsType = insertedRow.GetSmallInt("rls_type_");
                        foreach (string key in selects.Keys)
                        {
                            try
                            {
                                // Get the Select Object from the Dictionary.
                                Select select = selects[key];
                                // Lets Validate if the User is allowed to read this data.
                                // Lets get the RLS ID & Type of the Data inserted.

                                bool isAuthorized = false;

                                switch (rlsType)
                                {
                                    case 1: // Role Based RLS
                                        isAuthorized = new CloudDB(select.GetUser().GetToken()).ValidateRoleRLS(rlsID, CRUD.READ);
                                        break;
                                    case 2: // Group Based RLS
                                        isAuthorized = new CloudDB(select.GetUser().GetToken()).ValidateGroupRLS(rlsID, CRUD.READ);
                                        break;
                                    default:
                                        isAuthorized = false;
                                        break;
                                }

                                // Lets let the Listener be Given the data if allowed.
                                if (isAuthorized)
                                {
                                    // Send the data to the user.
                                    switch ((CRUD)Enum.Parse(typeof(CRUD), msgJObj["type"].ToString()))
                                    {
                                        case CRUD.WRITE:
                                            // Data is being inserted into the device.
                                            select.DataChanged(Select.DataChange.INSERTED, insertedRow);
                                            break;
                                        case CRUD.UPDATE:
                                            // Data is being update into the device.
                                            select.DataChanged(Select.DataChange.UPDATED, insertedRow);
                                            break;
                                        case CRUD.DELETE:
                                            // Data is being deleted from the device.
                                            select.DataChanged(Select.DataChange.DELETED, insertedRow);
                                            break;
                                    }
                                }
                            }
                            catch (Exception er)
                            {
                                // There was an Error.
                                cleanUpArr.Add(key);
                            }
                        }

                        // Cleanup of the selects.
                        // Lets Clean up the Display Size Array.
                        try
                        {
                            // Here we will Remove all the listeners that don't exist now.
                            foreach (string key in cleanUpArr)
                            {
                                selects.Remove(key);
                            }
                            selectListerners[msgJObj["table_name"].ToString()] = selects;
                        }
                        catch (Exception er)
                        {
                            // There was an Error.
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                MainWindow.Message("Local Sender Listener Err : " + e.Message + " : " + e.StackTrace);
            }
        }

        // This method will be used to send the data to the listening objects on an update into the DB.
        private static void SendToUpdateListeners(JObject msgJObj)
        {
            try
            {
                // Here we will parse the data to be sent.
                Row row = new Row();
                // Here we will add the required columns of data.
                // Including system columns to be inserted.
                // Get the Keys of the Columns.
                row.AddColumn("sync_id_", msgJObj["sync_id_"].ToString());

                // Get the Fresh data from the database.
                // Lets Create the Statement.
                SQLiteCommand dbSess = dbConn.GetCmd();
                // Reset the Session.
                dbSess.Reset();
                // Create the Statement.
                dbSess.CommandText = "SELECT * FROM " + (msgJObj["table_name"].ToString().Replace(" ", ""))
                            + " WHERE sync_id_ = @sync_id_";
                // Bind the Param.
                SQLiteParameter syncParam = new SQLiteParameter("sync_id_", msgJObj["sync_id_"].ToObject<object>());
                // Add the param to the command.
                dbSess.Parameters.Add(syncParam);
                // Lets prepare the statement.
                dbSess.Prepare();
                // Lets Execute the Statement.
                SQLiteDataReader sdr = dbSess.ExecuteReader();
                dbSess.Dispose();

                if (sdr.HasRows)
                {
                    // There is a row that we will have to create and send to the user.
                    // Create the Row.
                    row = SQLiteReaderToRows(sdr)[0];

                    // We will push the data to the listeners.
                    // Get the listeners according to the table
                    if (selectListerners.ContainsKey(msgJObj["table_name"].ToString()))
                    {
                        // There are data change listeners.
                        // Check the type of change in data.
                        Dictionary<string, Select> selects = selectListerners[msgJObj["table_name"].ToString()];

                        // Lets setup for Cleanup
                        List<string> cleanUpArr = new List<string>();

                        // Lets now send the data to the select objects.
                        foreach (string key in selects.Keys)
                        {
                            try
                            {
                                // Get the Select Object from the Dictionary.
                                Select select = selects[key];
                                // Send the data to the user.
                                switch ((CRUD)Enum.Parse(typeof(CRUD), msgJObj["type"].ToString()))
                                {
                                    case CRUD.WRITE:
                                        // Data is being inserted into the device.
                                        select.DataChanged(Select.DataChange.INSERTED, row);
                                        break;
                                    case CRUD.UPDATE:
                                        // Data is being update into the device.
                                        select.DataChanged(Select.DataChange.UPDATED, row);
                                        break;
                                    case CRUD.DELETE:
                                        // Data is being deleted from the device.
                                        select.DataChanged(Select.DataChange.DELETED, row);
                                        break;
                                }
                            }
                            catch (Exception er)
                            {
                                // There was an Error.
                                cleanUpArr.Add(key);
                            }
                        }

                        // Cleanup of the selects.
                        // Lets Clean up the Display Size Array.
                        try
                        {
                            // Here we will Remove all the listeners that don't exist now.
                            foreach (string key in cleanUpArr)
                            {
                                selects.Remove(key);
                            }
                            selectListerners[msgJObj["table_name"].ToString()] = selects;
                        }
                        catch (Exception er)
                        {
                            // There was an Error.
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // This method will be used to send the data to the listening objects on a delete from the DB.
        private static void SendToDeleteListeners(JObject msgJObj, Row deletedRow)
        {
            try
            {
                // Here we will send the data to the user.
                // We will push the data to the listeners.
                // Get the listeners according to the table
                if (selectListerners.ContainsKey(msgJObj["table_name"].ToString()))
                {
                    // There are data change listeners.
                    // Check the type of change in data.
                    Dictionary<string, Select> selects = selectListerners[msgJObj["table_name"].ToString()];

                    // Lets set up for cleanup.
                    List<string> cleanUpArr = new List<string>();

                    // Lets now send the data to the select objects.
                    foreach (string key in selects.Keys)
                    {
                        try
                        {
                            // Get the Select Object from the Dictionary.
                            Select select = selects[key];
                            // Send the data to the user.
                            switch ((CRUD)Enum.Parse(typeof(CRUD), msgJObj["type"].ToString()))
                            {
                                case CRUD.WRITE:
                                    // Data is being inserted into the device.
                                    select.DataChanged(Select.DataChange.INSERTED, deletedRow);
                                    break;
                                case CRUD.UPDATE:
                                    // Data is being update into the device.
                                    select.DataChanged(Select.DataChange.UPDATED, deletedRow);
                                    break;
                                case CRUD.DELETE:
                                    // Data is being deleted from the device.
                                    select.DataChanged(Select.DataChange.DELETED, deletedRow);
                                    break;
                            }
                        }
                        catch (Exception er)
                        {
                            // There was an Error.
                            cleanUpArr.Add(key);
                        }
                    }

                    // Cleanup of the selects.
                    // Lets Clean up the Display Size Array.
                    try
                    {
                        // Here we will Remove all the listeners that don't exist now.
                        foreach (string key in cleanUpArr)
                        {
                            selects.Remove(key);
                        }
                        selectListerners[msgJObj["table_name"].ToString()] = selects;
                    }
                    catch (Exception er)
                    {
                        // There was an Error.
                    }
                }
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // These methods will be used to sync the data with the cloud db server.
        // Will handle syncing the insert data into the cloud service.
        internal static void Sync(Insert insert)
        {
            try
            {
                // Here we will extract the required data and sync with the cloud server.
                // First we will extract the required data.
                JObject dataJObj = new JObject();
                // Lets Add the Column data.
                JObject colJObj = new JObject();
                Dictionary<string, object> columnData = insert.GetColumnData();
                Row newDataRow = new Row();    // This will be used to send the data to other selects listening for changes.

                // Lets get the data from the database.
                try
                {
                    // Here we will Get the Data from the Table.
                    bool extracted = false;
                    new Select(insert.GetUser().GetToken())
                        .From(insert.GetTableName())
                        .AddWhere(new Where("sync_id_", Where.Type.EQUAL, insert.GetSyncID()))
                        .SetOnDataResult((List<Row> insertedRows) =>
                        {
                            try
                            {
                                // Here we will Extract the data.
                                newDataRow = insertedRows[0];
                                extracted = true;
                            }
                            catch (Exception er)
                            {
                                // There was an Error.
                            }
                        })
                        .Execute();

                    // Lets now proceed with the new data.
                    if (extracted)
                    {
                        foreach (string columnName in newDataRow.GetColumnNames())
                        {
                            try
                            {
                                // Here we will extract the required data.
                                JProperty colJProp = new JProperty(columnName, newDataRow.GetValue(columnName));
                                colJObj.Add(colJProp);
                            }
                            catch (Exception e)
                            {
                                // There was an Error.
                            }
                        }
                    }
                }
                catch (Exception er)
                {
                    // There was an Error.
                }
                dataJObj.Add("columns", colJObj);

                // Lets add the Tenant ID.
                dataJObj.Add("tenant_id", insert.GetUser().GetTenantID());

                // Lets add the RLS Details.
                dataJObj.Add("rls_id_", insert.GetRLSID());
                dataJObj.Add("rls_type_", insert.GetRLSType());

                // Push the data to the select object that are listening for this change.
                try
                {
                    // Get the listeners according to the table
                    if (selectListerners.ContainsKey(insert.GetTableName()))
                    {
                        // There are data change listeners.
                        // Send the data to the users if required.
                        // Check the type of change in data.
                        Dictionary<string, Select> selects = selectListerners[insert.GetTableName()];

                        // Lets setup for cleanup.
                        List<string> cleanUpArr = new List<string>();

                        // Lets now send the data to the select objects.
                        foreach (string key in selects.Keys)
                        {
                            try
                            {
                                // Get the Select Object from the Dictionary.
                                Select select = selects[key];
                                // Send the data to the user.
                                select.DataChanged(Select.DataChange.INSERTED, newDataRow);
                            }
                            catch (Exception er)
                            {
                                // There was an Error.
                                cleanUpArr.Add(key);
                            }
                        }

                        // Cleanup of the selects.
                        // Lets Clean up the Display Size Array.
                        try
                        {
                            // Here we will Remove all the listeners that don't exist now.
                            foreach (string key in cleanUpArr)
                            {
                                selects.Remove(key);
                            }
                            selectListerners[insert.GetTableName()] = selects;
                        }
                        catch (Exception er)
                        {
                            // There was an Error.
                        }
                    }
                }
                catch (Exception e)
                {
                    // There was an Error.
                }

                // Initialize the Syncing Process on another thread.
                // Create another thread to maintain the sync.
                new Thread(() =>
                {
                    try
                    {
                        // Now we will store the data that has to be synced into the system tables to maintain offline syncing.
                        // Store into the Sync Offline Table.
                        try
                        {
                            // Insert into the Sync Offline.
                            new Insert(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                .IntoSystem("sync_offline_")
                                .PutColumn(new ColumnData("id").Put(insert.GetSyncID()))
                                .PutColumn(new ColumnData("crud_type").Put("INSERT"))
                                .PutColumn(new ColumnData("table_name").Put(insert.GetTableName()))
                                .PutColumn(new ColumnData("data").Put(dataJObj.ToString()))
                                .PutColumn(new ColumnData("token").Put(insert.GetUser().GetToken()))
                                .SetUpdateTime(newDataRow.GetBigInt("update_time_"))
                                .SetSyncable(false)
                                .Execute();  // Execute the Offline Sync table.
                        }
                        catch (Exception er)
                        {
                            // There was an Error.
                        }

                        // Now lets sync into the Sync Check Table.
                        try
                        {
                            // Lets sync into the Sync Check Table.
                            new Insert(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                .IntoSystem("synced_check_")
                                .PutColumn(new ColumnData("id").Put(insert.GetSyncID()))
                                .PutColumn(new ColumnData("crud_type").Put("INSERT"))
                                .PutColumn(new ColumnData("table_name").Put(insert.GetTableName()))
                                .PutColumn(new ColumnData("data").Put(dataJObj.ToString()))
                                .PutColumn(new ColumnData("is_self_written").Put(true))
                                .SetUpdateTime(newDataRow.GetBigInt("update_time_"))
                                .SetSyncable(false)
                                .Execute();
                        }
                        catch (Exception er)
                        {
                            // There was an Error.
                        }

                        // Create the Waiter object.
                        // Set up the call back function to delete from the system table and maintain the syncing.
                        // Only make the call if there is an internet connection.
                        if (PushManager.NetworkStatus == PushManager.Network.CONNECTED)
                        {
                            new Waiter()
                                .Url(C.CLOUD_DB_IP_ADDRESS)
                                .Endpoint("CloudDB/Table/Data")
                                .Method(Waiter.CallMethod.POST)
                                .AddParam(new ParamData("columns").Put(colJObj))
                                .AddParam(new ParamData("rls_id_").Put(insert.GetRLSID()))
                                .AddParam(new ParamData("rls_type_").Put(insert.GetRLSType()))
                                .AddParam(new ParamData("table_name").Put(insert.GetTableName()))
                                .AddParam(new ParamData("token").Put(insert.GetUser().GetToken()))
                                .AddParam(new ParamData("sync_id").Put(insert.GetSyncID()))
                                .AddParam(new ParamData("v").Put(C.VERSION_0_1))
                                .AddParam(new ParamData("update_time_").Put(newDataRow.GetBigInt("update_time_")))
                                .SetUID(insert.GetSyncID() + newDataRow.GetBigInt("update_time_"))
                                // Set up the Callback.
                                .SetOnResponse((bool isSuccess, string response) =>
                                {
                                    try
                                    {
                                        // Here we will perform actions on the response.
                                        // Here we will Delete the Data from the Sync System Table.
                                        // Lets validate the response.
                                        if (isSuccess)
                                        {
                                            breadCrumb.BC.Info(insert.GetTableName() + " Data Synced");
                                            JObject respJObj = JObject.Parse(response);
                                            if (respJObj.ContainsKey("success"))
                                            {
                                                // The data was properly inserted.
                                                new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                                        .FromSystem("sync_offline_")
                                                        .AddWhere(new Where("id", Where.Type.EQUAL, insert.GetSyncID()))
                                                        .AddWhere(new Where("update_time_", Where.Type.EQUAL,
                                                            newDataRow.GetBigInt("update_time_")))
                                                        .Execute();
                                            }
                                        }
                                        else
                                        {
                                            //breadCrumb.BC.Warn(insert.GetTableName() + " Data Not Synced");
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        // Here we have an Error.
                                        breadCrumb.BC.Error(insert.GetTableName() + " Data Sync Error : " + e.Message + " : " + e.StackTrace);
                                    }
                                }).Execute();
                        }
                    }
                    catch (Exception e)
                    {
                        // There was an Error in the Syncing process.
                    }
                }).Start();
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // Will handle syncing the update data into the cloud service.
        internal static void Sync(Update update, List<Row> updatedRows)
        {
            try
            {
                // Here we will extract the required data and sync with the cloud server.
                // First we will extract the required data.
                // Push the data to the select object that are listening for this change.
                try
                {
                    // Get the listeners according to the table
                    if (selectListerners.ContainsKey(update.GetTableName()))
                    {
                        // There are data change listeners.
                        // Send the data to the users if required.
                        // Check the type of change in data.
                        Dictionary<string, Select> selects = selectListerners[update.GetTableName()];

                        // Lets setup for cleanup
                        List<string> cleanUpArr = new List<string>();

                        // Lets now send the data to the select objects.
                        foreach (string key in selects.Keys)
                        {
                            try
                            {
                                // Get the Select Object from the Dictionary.
                                Select select = selects[key];
                                // Send the rows to the user.
                                for (int i = 0; i < updatedRows.Count; i++)
                                {
                                    // Send the Data to the Select Listener one row at a time.
                                    select.DataChanged(Select.DataChange.UPDATED, updatedRows[i]);
                                }
                            }
                            catch (Exception er)
                            {
                                // There was an Error.
                                cleanUpArr.Add(key);
                            }
                        }

                        // Cleanup of the selects.
                        // Lets Clean up the Display Size Array.
                        try
                        {
                            // Here we will Remove all the listeners that don't exist now.
                            foreach (string key in cleanUpArr)
                            {
                                selects.Remove(key);
                            }
                            selectListerners[update.GetTableName()] = selects;
                        }
                        catch (Exception er)
                        {
                            // There was an Error.
                        }
                    }
                }
                catch (Exception e)
                {
                    // There was an Error.
                }

                JObject dataJObj = new JObject();
                // Lets Add the Column data.
                JObject colJObj = update.GetColumnJObj();
                dataJObj.Add("columns", colJObj);

                // Lets Add the Where Data.
                JObject whereJObj = update.GetWhereJObj();
                dataJObj.Add("wheres", whereJObj);

                // Initialize the Syncing Process on another thread.
                // Create another thread to maintain the sync.
                new Thread(() =>
                {
                    try
                    {
                        // Now we will store the data that has to be synced into the system tables to maintain offline syncing.
                        // Store into the Sync Offline Table.
                        string newUUID = Guid.NewGuid().ToString();
                        new Insert(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                            .IntoSystem("sync_offline_")
                            .PutColumn(new ColumnData("id").Put(newUUID))
                            .PutColumn(new ColumnData("crud_type").Put("UPDATE"))
                            .PutColumn(new ColumnData("table_name").Put(update.GetTableName()))
                            .PutColumn(new ColumnData("data").Put(dataJObj.ToString()))
                            .PutColumn(new ColumnData("token").Put(update.GetUser().GetToken()))
                            .SetUpdateTime(update.GetUpdateTime())
                            .SetSyncable(false)
                            .Execute();  // Execute the Offline Sync table.

                        // Now lets sync into the Sync Check Table.
                        // Now lets insert all the updates that are happening.
                        for (int i = 0; i < updatedRows.Count; i++)
                        {
                            try
                            {
                                // Lets insert the Update into the database to make sure there are no double entries.
                                new Insert(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                    .IntoSystem("synced_check_")
                                    .PutColumn(new ColumnData("id").Put(updatedRows[i].GetString("sync_id_")))
                                    .PutColumn(new ColumnData("crud_type").Put("UPDATE"))
                                    .PutColumn(new ColumnData("table_name").Put(update.GetTableName()))
                                    .PutColumn(new ColumnData("data").Put(dataJObj.ToString()))
                                    .PutColumn(new ColumnData("is_self_written").Put(true))
                                    .SetUpdateTime(update.GetUpdateTime())
                                    .SetSyncable(false)
                                    .Execute();
                            }
                            catch (Exception e)
                            {
                                // There was an Error.
                            }
                        }

                        // Create the Waiter object.
                        // Set up the call back function to delete from the system table and maintain the syncing.
                        new Waiter()
                            .Url(C.CLOUD_DB_IP_ADDRESS)
                            .Endpoint("CloudDB/Table/Data")
                            .Method(Waiter.CallMethod.PUT)
                            .AddParam(new ParamData("v").Put(C.VERSION_0_1))
                            .AddParam(new ParamData("table_name").Put(update.GetTableName()))
                            .AddParam(new ParamData("token").Put(update.GetUser().GetToken()))
                            .AddParam(new ParamData("columns").Put(colJObj))
                            .AddParam(new ParamData("wheres").Put(whereJObj))
                            .AddParam(new ParamData("update_time_").Put(update.GetUpdateTime()))
                            .SetUID(newUUID + update.GetUpdateTime())
                            // Set up the Callback.
                            .SetOnResponse((bool isSuccess, string response) =>
                            {
                                try
                                {
                                    // Here we will perform actions on the response.
                                    // Here we will Delete the Data from the Sync System Table.
                                    // Lets validate the response.
                                    if (isSuccess)
                                    {
                                        JObject respJObj = JObject.Parse(response);
                                        if (respJObj.ContainsKey("success"))
                                        {
                                            // The data was properly Updated.
                                            new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                                .FromSystem("sync_offline_")
                                                .AddWhere(new Where("id", Where.Type.EQUAL, newUUID))
                                                .AddWhere(new Where("update_time_", Where.Type.EQUAL, update.GetUpdateTime()))
                                                .Execute();
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    // Here we have an Error.
                                }
                            }).Execute();
                    }
                    catch (Exception e)
                    {
                        // There was an Error in the Syncing process.
                    }
                }).Start();
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // Will handle syncing the deleted data into the cloud service.
        internal static void Sync(Delete delete, List<Row> deletedRows)
        {
            try
            {
                // Here we will extract the required data and sync with the cloud server.
                // First we will extract the required data.
                JObject dataJObj = new JObject();
                // Lets Add the Where Data.
                JObject whereJObj = delete.GetWhereJObj();
                dataJObj.Add("wheres", whereJObj);
                // Push the data to the select object that are listening for this change.
                try
                {
                    // Get the listeners according to the table
                    if (selectListerners.ContainsKey(delete.GetTableName()))
                    {
                        // There are data change listeners.
                        // Send the data to the users if required.
                        // Check the type of change in data.
                        Dictionary<string, Select> selects = selectListerners[delete.GetTableName()];

                        // Setup for cleanup.
                        List<string> cleanUpArr = new List<string>();

                        // Lets now send the data to the select objects.
                        foreach (string key in selects.Keys)
                        {
                            try
                            {
                                // Get the Select Object from the Dictionary.
                                Select select = selects[key];
                                // Here we will send the data of all the rows that have been deleted to the user.
                                for (int i = 0; i < deletedRows.Count; i++)
                                {
                                    // Send the data to the user.
                                    select.DataChanged(Select.DataChange.DELETED, deletedRows[i]);
                                }
                            }
                            catch (Exception er)
                            {
                                // There was an Error.
                                cleanUpArr.Add(key);
                            }
                        }

                        // Cleanup of the selects.
                        // Lets Clean up the Display Size Array.
                        try
                        {
                            // Here we will Remove all the listeners that don't exist now.
                            foreach (string key in cleanUpArr)
                            {
                                selects.Remove(key);
                            }
                            selectListerners[delete.GetTableName()] = selects;
                        }
                        catch (Exception er)
                        {
                            // There was an Error.
                        }
                    }
                }
                catch (Exception e)
                {
                    // There was an Error.
                }

                // Initialize the Syncing Process on another thread.
                // Create another thread to maintain the sync.
                new Thread(() =>
                {
                    try
                    {
                        // Now we will store the data that has to be synced into the system tables to maintain offline syncing.
                        // Store into the Sync Offline Table.
                        string newGuid = Guid.NewGuid().ToString();
                        Insert offlineSync = new Insert(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                            .IntoSystem("sync_offline_")
                            .PutColumn(new ColumnData("id").Put(newGuid))
                            .PutColumn(new ColumnData("crud_type").Put("DELETE"))
                            .PutColumn(new ColumnData("table_name").Put(delete.GetTableName()))
                            .PutColumn(new ColumnData("data").Put(dataJObj.ToString()))
                            .PutColumn(new ColumnData("token").Put(delete.GetUser().GetToken()))
                            .SetUpdateTime(delete.GetDeletedTime())
                            .SetSyncable(false);
                        // Execute the Offline Sync table.
                        offlineSync.Execute();

                        // Now lets sync into the Sync Check Table.
                        for (int i = 0; i < deletedRows.Count; i++)
                        {
                            offlineSync.IntoSystem("synced_check_")
                                .PutColumn(new ColumnData("id").Put(deletedRows[i].GetString("id")))
                                .PutColumn(new ColumnData("crud_type").Put("DELETE"))
                                .PutColumn(new ColumnData("table_name").Put(delete.GetTableName()))
                                .PutColumn(new ColumnData("data").Put(dataJObj.ToString()))
                                .PutColumn(new ColumnData("is_self_written").Put(true))
                                .SetSyncable(false)
                                .SetUpdateTime(delete.GetDeletedTime())
                                .Execute();
                        }

                        // Create the Waiter object.
                        // Set up the call back function to delete from the system table and maintain the syncing.
                        new Waiter()
                            .Url(C.CLOUD_DB_IP_ADDRESS)
                            .Endpoint("CloudDB/Table/Data")
                            .Method(Waiter.CallMethod.DELETE)
                            .AddParam(new ParamData("v").Put(C.VERSION_0_1))
                            .AddParam(new ParamData("token").Put(delete.GetUser().GetToken()))
                            .AddParam(new ParamData("table_name").Put(delete.GetTableName()))
                            .AddParam(new ParamData("wheres").Put(whereJObj))
                            .AddParam(new ParamData("delete_time_").Put(delete.GetDeletedTime()))
                            .SetUID(newGuid + delete.GetDeletedTime())
                            // Set up the Callback.
                            .SetOnResponse((bool isSuccess, string response) =>
                            {
                                try
                                {
                                    // Here we will perform actions on the response.
                                    // Here we will Delete the Data from the Sync System Table.
                                    // Lets validate the response.
                                    if (isSuccess)
                                    {
                                        JObject respJObj = JObject.Parse(response);
                                        if (respJObj.ContainsKey("success"))
                                        {
                                            // The data was properly Updated.
                                            new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                                .FromSystem("sync_offline_")
                                                .AddWhere(new Where("id", Where.Type.EQUAL, newGuid))
                                                .AddWhere(new Where("update_time_", Where.Type.EQUAL, delete.GetDeletedTime()))
                                                .ExecuteAsync();
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    // Here we have an Error.
                                }
                            }).Execute();
                    }
                    catch (Exception e)
                    {
                        // There was an Error in the Syncing process.
                    }
                }).Start();
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // Add the Data Changed Listeners here.
        internal static void SetSelectListener(Select select)
        {
            try
            {
                // Here we will set the cloud db object into the dictionary we have.
                // Check if there are other select object listening to the same table changes.
                if (selectListerners.ContainsKey(select.GetTableName()))
                {
                    // There are other select objects listening for the same table changes.
                    // Check if the ID exists.
                    if (selectListerners[select.GetTableName()].ContainsKey(select.GetIdentifier()))
                    {
                        // There exists another of the same id as well.
                        selectListerners[select.GetTableName()][select.GetIdentifier()] = select;
                    }
                    else
                    {
                        // No Select Exists with the same id.
                        selectListerners[select.GetTableName()].Add(select.GetIdentifier(), select);
                    }
                }
                else
                {
                    // There is no listening selects on this table.
                    Dictionary<string, Select> selectMap = new Dictionary<string, Select>();
                    selectMap.Add(select.GetIdentifier(), select);
                    selectListerners.Add(select.GetTableName(), selectMap);
                }
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // This will be used to close the Cloud Listener.
        internal static void CloseListener(string tableName, string cloudDBID)
        {
            try
            {
                // Lets try to remove the Cloud DB object from the Dictionary.
                if (selectListerners.ContainsKey(tableName))
                {
                    if (selectListerners[tableName].ContainsKey(cloudDBID))
                    {
                        selectListerners[tableName].Remove(cloudDBID);
                    }
                }
            }
            catch (Exception e)
            {
                // There was an Error
            }
        }

        // Here we set the status and the status trace.
        private static void AddStatus(string status1)
        {
            try
            {
                // Here we append the status to the status trace and status.
                if (StatusTrace == null)
                {
                    StatusTrace = new StringBuilder();
                }
                Status = status1;
                StatusTrace.Append(status1)
                    .Append('\n');
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // This will retrieve the whole status trace.
        public static string GetStatusTrace()
        {
            return StatusTrace.ToString();
        }

        // This will retrieve just the status of the cloud object.
        public static string GetStatus()
        {
            return Status;
        }

        /* This class will handle the connection to the database. */
        // We will only have one connection to the local database.
        protected class DBConnector
        {
            // Variables.
            internal string status { get; set; }
            internal StringBuilder statusTrace;
            internal bool foreignKeyEnabled { get; set; }

            // Static Variables
            private static SQLiteConnection conn { get; set; }

            // Constants
            public const string DEFAULT_DB = "a36e_biz";

            // Here we will initialize all the static variables for access without any instance creation.
            internal static string Init()
            {
                try
                {
                    // Here we will initialize the required variables.
                    // Establish a connection to the database if required.
                    if (conn == null)
                    {
                        conn = new SQLiteConnection("Data Source=" + DEFAULT_DB + ".db");
                        conn.Open();
                    }
                    return "Initalized";
                }
                catch (Exception e)
                {
                    // There was an Error.
                    return "Error : " + e.Message;
                }
            }

            // Default Connstructor.
            // This is the main database.
            // Will Hold all the main company related data.
            // This will not hold data that will be within the company.
            public DBConnector()
            {
                try
                {
                    // Constructor of the Database Helper.
                    if (conn == null)
                    {
                        conn = new SQLiteConnection("Data Source=" + DEFAULT_DB + ".db");
                        conn.Open();
                    }

                }
                catch (Exception e)
                {
                    // There was an error in opening connection to database.
                    status = "Error : " + e.Message;
                }
            }

            // Create the DB command.
            public SQLiteCommand GetCmd()
            {
                try
                {
                    try
                    {
                        // Create and connect the sql commands
                        SQLiteCommand dbSess = new SQLiteCommand(null, conn);
                        // Lets Initialize the Foreign Key Support.
                        foreignKeyEnabled = true;
                        // Enable Foreign Key Support for Database Session.
                        dbSess.CommandText = "PRAGMA foreign_keys = ON;";
                        foreignKeyEnabled = dbSess.ExecuteNonQuery() > 0;
                        return dbSess;
                    }
                    catch (StackOverflowException e)
                    {
                        // There was an Error.
                        status = "Stack Overflow Err : " + e.Message;
                        foreignKeyEnabled = false;
                        return null;
                    }
                    catch (Exception e)
                    {
                        // There was an Error.
                        status = "Foreign Key Err : " + e.Message;
                        foreignKeyEnabled = false;
                        return null;
                    }
                }
                catch (Exception e)
                {
                    // There was an Error.
                    return null;
                }
            }

            // Close the Connection to the local database.
            public bool Close()
            {
                try
                {
                    // Here we will try to close the connection with the local database.
                    // Close the Connection to the database.
                    conn.Close();
                    conn = null;
                    return true;
                }
                catch (Exception e)
                {
                    // There was an error.
                    return false;
                }
            }

            // Maintain the Status of the System.
            private void AddStatus(string newStatus)
            {
                try
                {
                    // Here we append the status to the status trace and status.
                    if (statusTrace == null)
                    {
                        statusTrace = new StringBuilder();
                    }
                    status = newStatus;
                    statusTrace.Append(newStatus)
                        .Append('\n');
                }
                catch (Exception e)
                {
                    // There was an Error.
                }
            }

            // This will retrieve the whole status trace.
            public string GetStatusTrace()
            {
                return statusTrace.ToString();
            }

            // This will retrieve just the status of the cloud object.
            public string GetStatus()
            {
                return status;
            }
        }
    }
}

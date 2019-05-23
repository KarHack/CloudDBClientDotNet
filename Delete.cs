using _36E_Business___ERP.helper;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace _36E_Business___ERP.cloudDB
{
    class Delete
    {

        // Variables.
        private CloudDB cdb;
        private string tableName;
        private StringBuilder whereClauseBuilder;
        private Dictionary<string, object> whereBindObjs;
        private long tenantID;
        private short rlsType;
        private long rlsID;
        private bool isError = false;
        private bool syncable = true;
        private string syncID;
        private string status;
        private StringBuilder statusTrace;
        private JObject wheresJObj;
        private long deletedTimeI = 0;

        // Callbacks.
        // This will be used to send the insert number to the user if the listener is set.
        public delegate void OnDataDeleted(List<Row> deletedRows);
        private OnDataDeleted onDataDeleted = null;

        // Constructor.
        // The Cloud DB Constructor.
        public Delete(CloudDB cdb)
        {
            try
            {
                // Authorize the User.
                // Initialize the Cloud DB Service.
                if (cdb.IsSystemUser || cdb.user.IsActive)
                {
                    this.cdb = cdb;     // Set the Cloud DB Service.
                    wheresJObj = new JObject();
                    // The User is authorized
                    AddStatus("User Authenticated");
                }
                else
                {
                    // The user is not authorized.
                    isError = true;
                    AddStatus("User not Authenticated");
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                isError = true;
                AddStatus("Error : " + e.Message);
            }
        }

        // The User Token Constructor.
        public Delete(string token)
        {
            try
            {
                // Authorize the User.
                // Initialize the Cloud DB Service.
                CloudDB cdb = new CloudDB(token);
                if (cdb.IsSystemUser || cdb.user.IsActive)
                {
                    this.cdb = cdb;     // Set the Cloud DB Service.
                    wheresJObj = new JObject();
                    // The User is authorized
                    AddStatus("User Authenticated");
                }
                else
                {
                    // The user is not authorized.
                    isError = true;
                    AddStatus("User not Authenticated");
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                isError = true;
                AddStatus("Error : " + e.Message);
            }
        }

        // This Constructor we will use to set the table name.
        public Delete From(string tableName)
        {
            try
            {
                // Set the Table Name.
                // Authentication.
                if (cdb.ValidateCRUD(tableName, CloudDB.CRUD.DELETE) && cdb.user.IsActive)
                {
                    this.tableName = Regex.Replace(tableName, " ", "");
                    // Initiate the Required Variables.
                    whereClauseBuilder = new StringBuilder();
                    whereBindObjs = new Dictionary<string, object>();
                    AddStatus("User Authorized to Delete from the Table");
                    if (CloudDB.IsSyncable(tableName))
                    {
                        // The table is syncable.
                        // Lets not do anything here, as the user might not want to sync this table.
                    }
                    else
                    {
                        // This table is not syncable.
                        syncable = false;
                    }
                    return this;
                }
                else
                {
                    // The user is not authorized to delete from this table.
                    isError = true;
                    AddStatus("User NOT Authorized to Delete from the Table");
                    return this;
                }

            }
            catch (Exception e)
            {
                // There was an Error.
                isError = true;
                AddStatus("Error : " + e.Message);
                return this;
            }
        }

        // Add the Table Name of the System Tables.
        public Delete FromSystem(string tableName)
        {
            try
            {
                // Set the Table Name.
                // Authentication.
                if (this.tableName == null && Helper.DBValidation.IsSystemTB(tableName) && cdb.IsSystemUser)
                {
                    this.tableName = Regex.Replace(tableName, " ", "");
                    // Initiate the Required Variables.
                    whereClauseBuilder = new StringBuilder();
                    whereBindObjs = new Dictionary<string, object>();
                    AddStatus("User Authorized to Delete from the Table");
                    return this;
                }
                else
                {
                    // The user is not authorized to delete from this table.
                    isError = true;
                    AddStatus("User NOT Authorized to Delete from the Table");
                    return this;
                }

            }
            catch (Exception e)
            {
                // There was an Error.
                isError = true;
                AddStatus("Error : " + e.Message);
                return this;
            }
        }

        // Set the Where Clauses here.
        public Delete AddWhere(Where clause)
        {
            try
            {
                // Add the where to the system, with the default connector.
                int connType = 0;
                if (whereClauseBuilder.Length > 0)
                {
                    whereClauseBuilder.Append(clause.GetConnector());
                    connType = clause.GetConnector() == "and" ? 1 : 2;
                }
                whereClauseBuilder.Append(clause.GetColumnName())
                    .Append(clause.GetOperator())
                    .Append('@')
                    .Append(clause.GetColumnName())
                    .Append(' ');
                whereBindObjs.Add(clause.GetColumnName(), clause.GetColumnData());

                // Here we will add the Where to the Where JSON Object.
                try
                {
                    if (syncable)
                    {
                        // Here we will Add the Json Object.
                        JObject whereJObj = new JObject
                        {
                            { "name", clause.GetColumnName() }
                        };
                        switch (clause.GetOperatorType())
                        {
                            case Where.Type.EQUAL:
                                whereJObj.Add("condition", "EQUAL");
                                break;
                            case Where.Type.GREATER:
                                whereJObj.Add("condition", "GREATER");
                                break;
                            case Where.Type.GREATER_EQUAL:
                                whereJObj.Add("condition", "GREATER_EQUAL");
                                break;
                            case Where.Type.LESSER:
                                whereJObj.Add("condition", "LESSER");
                                break;
                            case Where.Type.LESSER_EQUAL:
                                whereJObj.Add("condition", "LESSER_EQUAL");
                                break;
                        }
                        whereJObj.Add("data", JToken.FromObject(clause.GetColumnData()));
                        // Lets add the JSON Object to the Wheres Json Object.
                        if (connType == 0)
                        {
                            // This is the First Where.
                            wheresJObj = whereJObj;
                        }
                        else if (connType == 1)
                        {
                            // This is the And insert.
                            wheresJObj.Add("and", whereJObj);
                        }
                        else if (connType == 2)
                        {
                            // This is the OR insert.
                            wheresJObj.Add("or", whereJObj);
                        }
                    }
                }
                catch (Exception er)
                {
                    // There was an Error.
                }

                return this;
            }
            catch (Exception e)
            {
                // There was an Error.
                return this;
            }
        }

        // Set the Role ID of the Row to be inserted.
        // Has to be a role of the user.
        public Delete SetRoleID(long roleID)
        {
            try
            {
                // Here we will Add the Role ID given by the User to the Statement.
                // Check if the Table is syncable, then only Add the row level security.
                if (CloudDB.IsSyncable(tableName))
                {
                    // Lets validate if the User is allowed to write with this role.
                    if (cdb.ValidateRoleRLS(roleID, CloudDB.CRUD.DELETE))
                    {
                        // The user is allowed to write with this role.
                        rlsType = C.RLS.ROW_RLS;
                        rlsID = roleID;
                        return this;
                    }
                    else
                    {
                        // The user is not allowed to write with this role.
                        rlsType = C.RLS.NO_RLS;
                        rlsID = 0;
                        isError = true;
                        AddStatus("User not Authorized to Delete from this Role");
                        return this;
                    }

                }
                else
                {
                    // The table is not syncable, so we will not Add any RLS.
                    rlsType = C.RLS.NO_RLS;
                    rlsID = 0;
                    return this;
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                rlsType = C.RLS.NO_RLS;
                rlsID = 0;
                isError = true;
                AddStatus("Error : " + e.Message);
                return this;
            }
        }

        // Set the Group ID of the Row to be inserted.
        // Has to be a Group of the user.
        public Delete SetGroupID(long groupID)
        {
            try
            {
                // Here we will Add the Group ID given by the User to the Statement.
                // Check if the Table is syncable, then only Add the row level security.
                if (CloudDB.IsSyncable(tableName))
                {
                    // Lets validate if the User is allowed to Delete with this role.
                    if (cdb.ValidateGroupRLS(groupID, CloudDB.CRUD.DELETE))
                    {
                        // The user is allowed to write with this role.
                        rlsType = C.RLS.GROUP_RLS;
                        rlsID = groupID;
                        return this;
                    }
                    else
                    {
                        // The user is not allowed to write with this Group.
                        rlsType = C.RLS.NO_RLS;
                        rlsID = 0;
                        isError = true;
                        AddStatus("User not Authorized to Delete from this Group");
                        return this;
                    }

                }
                else
                {
                    // The table is not syncable, so we will not Add any RLS.
                    rlsType = C.RLS.NO_RLS;
                    rlsID = 0;
                    return this;
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                rlsType = C.RLS.NO_RLS;
                rlsID = 0;
                isError = true;
                AddStatus("Error : " + e.Message);
                return this;
            }
        }

        // Let the user to suggest which rls to add to in one method only.
        public Delete SetRLSID(short rowLevelSecurityType, long rowLevelSecurityID)
        {
            try
            {
                // Here we will Set the RLS ID.
                // Here we will Add the Group ID given by the User to the Statement.
                // Check if the Table is syncable, then only Add the row level security.
                if (CloudDB.IsSyncable(tableName))
                {
                    // Lets check according to the RLS Type.
                    switch (rowLevelSecurityType)
                    {
                        // Its a Role Based RLS.
                        case 1:
                            // Lets validate if the User is allowed to write with this role.
                            if (cdb.ValidateRoleRLS(rowLevelSecurityID, CloudDB.CRUD.DELETE))
                            {
                                // The user is allowed to Delete with this role.
                                rlsType = C.RLS.ROW_RLS;
                                rlsID = rowLevelSecurityID;
                                AddStatus("User is Authorized to Delete in this Role");
                                return this;
                            }
                            else
                            {
                                // The user is not allowed to Delete with this role.
                                rlsType = C.RLS.NO_RLS;
                                rlsID = 0;
                                AddStatus("User is NOT Authorized to Delete in this Role");
                                isError = true;
                                return this;
                            }
                        // Its a Group Based RLS.
                        case 2:
                            // Lets validate if the User is allowed to Delete with this role.
                            if (cdb.ValidateGroupRLS(rowLevelSecurityID, CloudDB.CRUD.DELETE))
                            {
                                // The user is allowed to Delete with this role.
                                rlsType = C.RLS.GROUP_RLS;
                                rlsID = rowLevelSecurityID;
                                AddStatus("User is Authorized to Delete in this Group");
                                return this;
                            }
                            else
                            {
                                // The user is not allowed to Delete with this role.
                                rlsType = C.RLS.NO_RLS;
                                rlsID = 0;
                                isError = true;
                                AddStatus("User is NOT Authorized to Delete in this Group");
                                return this;
                            }
                        default:
                            // No RLS Type was given.
                            rlsType = C.RLS.NO_RLS;
                            rlsID = 0;
                            AddStatus("Invalid RLS Type Given");
                            isError = true;
                            return this;
                    }
                }
                else
                {
                    // The table is not syncable, so we will not Add any RLS.
                    rlsType = C.RLS.NO_RLS;
                    rlsID = 0;
                    AddStatus("Table is not a Syncable Table.");
                    return this;
                }
            }
            catch (Exception er)
            {
                // There was an Error.
                rlsType = C.RLS.NO_RLS;
                rlsID = 0;
                AddStatus("Error : " + er.Message);
                isError = true;
                return this;
            }
        }

        // A Mimic of the Above Method.
        public Delete SetRowLevelSecurityID(short rowLevelSecurityType, long rowLevelSecurityID)
        {
            return SetRLSID(rowLevelSecurityType, rowLevelSecurityID);
        }


        // Here we will set syncable to false, if the user wants to.
        public Delete SetSyncable(bool syncable)
        {
            this.syncable = syncable;
            return this;
        }

        // Set the Callback Listeners for the User.
        public Delete SetOnDataDeleted(OnDataDeleted onDataDeleted)
        {
            this.onDataDeleted = onDataDeleted;
            return this;
        }

        // Get the Deleted Time of the Rows.
        public long GetDeletedTime()
        {
            return deletedTimeI;
        }

        // Allow the User to Get the SQL Statement Generated.
        public string GetSQL()
        {
            try
            {
                // Validate if the User can proceed with the delete.
                // Attach the Main Statement.
                StringBuilder sqlBuilder = new StringBuilder();
                sqlBuilder.Append("DELETE FROM ")
                    .Append(tableName);

                // Add the Where Clauses.
                if (whereClauseBuilder.Length > 0)
                {
                    sqlBuilder.Append(" WHERE ")
                        .Append(whereClauseBuilder);

                    if (CloudDB.IsSyncable(tableName))
                    {
                        string rlsWhere = cdb.RLSWhere(CloudDB.QueryType.DELETE);
                        if (rlsWhere.Trim().Length > 0)
                        {
                            sqlBuilder.Append(" AND ")
                                .Append(rlsWhere);
                        }
                    }
                }
                else if (CloudDB.IsSyncable(tableName))
                {
                    string rlsWhere = cdb.RLSWhere(CloudDB.QueryType.DELETE);
                    if (rlsWhere.Trim().Length > 0)
                    {
                        sqlBuilder.Append(" WHERE ")
                            .Append(rlsWhere);
                    }
                }

                return sqlBuilder.ToString();
            }
            catch (Exception e)
            {
                // There was an Error.
                return "Error : " + e.Message;
            }
        }

        // Here we will get the user related to this delete.
        internal User GetUser()
        {
            try
            {
                // Here we will return the user related to this delete.
                return cdb.IsSystemUser ? null : cdb.user;
            }
            catch (Exception e)
            {
                // There was an Error
                return null;
            }
        }

        // Here we will Execute the Statement.
        // Executing the Statement in Synchornous Mode.
        public void Execute()
        {
            try
            {
                // Here we will Execute the query synchronously.
                InternalExecutor();
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // Executing the Statement in A-Synchornous Mode.
        public void ExecuteAsync()
        {
            try
            {
                // Here we will Execute the query asynchronously.
                // Create the Thread.
                Thread deleteExecutionThread = new Thread(InternalExecutor);
                // Start the Thread.
                deleteExecutionThread.Start();
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // This method will handle the actual execution and will me called.
        private void InternalExecutor()
        {
            try
            {
                // Here we will Execute the Query.
                if (IsSuccessful())
                {
                    SQLiteCommand dbSess = cdb.GetSession();

                    // Lets set the deleted time. (This will be used to reject any deletes trying to be run twice)
                    deletedTimeI = Helper.CurrentTimeMillis();

                    try
                    {
                        // Here we will Generate the SQL Statement.
                        // Add the RLS System.
                        if (CloudDB.IsSyncable(tableName))
                        {
                            // The table required RLS.
                            if (rlsID == 0)
                            {
                                // The user should not be allowed to execute this statement.
                                AddStatus("RLS NOT Provided");
                            }
                            else
                            {
                                // The RLS is Provided
                                AddWhere(new Where("rls_id_", Where.Type.EQUAL, rlsID));
                                AddWhere(new Where("rls_type_", Where.Type.EQUAL, rlsType));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        // There was an Error.
                        isError = true;
                        AddStatus("SQL Security : " + e.Message);
                    }

                    // Lets create the Statement.
                    dbSess.CommandText = GetSQL();
                    // Bind the Params of the Where clause.
                    foreach (string paramName in whereBindObjs.Keys)
                    {
                        // Bind the Params.
                        SQLiteParameter sqlParam = new SQLiteParameter(paramName, whereBindObjs[paramName]);
                        // Add the Param to the Command.
                        dbSess.Parameters.Add(sqlParam);
                    }
                    // Lets prepare the statement.
                    dbSess.Prepare();
                    // Lets Get the Data that will be deleted.
                    List<Row> deletedRows = new List<Row>();
                    try
                    {
                        // Lets get the updated rows.
                        // Lets Create the Statement to read from the updated table.
                        StringBuilder sqlBuilder = new StringBuilder();
                        sqlBuilder.Append("SELECT * FROM ")
                            .Append(tableName)
                            .Append(" WHERE ")
                            .Append(whereClauseBuilder);
                        // Lets Get the Session Object.
                        SQLiteCommand readSess = cdb.GetSession();
                        // Create the Statement.
                        readSess.CommandText = sqlBuilder.ToString();
                        // Bind the Columns Params.
                        foreach (string paramName in whereBindObjs.Keys)
                        {
                            // Bind the Params.
                            SQLiteParameter sqlParam = new SQLiteParameter(paramName, whereBindObjs[paramName]);
                            // Add the Param to the Command.
                            readSess.Parameters.Add(sqlParam);
                        }
                        // Lets Prepare the Statement.
                        readSess.Prepare();
                        // Lets execute the query.
                        SQLiteDataReader sdr = readSess.ExecuteReader();
                        // Lets read the data and add the data to the Rows List.
                        deletedRows = CloudDB.SQLiteReaderToRows(sdr);
                        // Reset the Session.
                        readSess.Reset();
                    }
                    catch (Exception e)
                    {
                        // There was an Error.
                    }
                    // Lets Execute the Query.
                    // The user is not asking to create a reader, just to insert the value.
                    int deletedRowsNum = dbSess.ExecuteNonQuery();

                    // Send to the Receiver if the user has set a insert result listener.
                    onDataDeleted?.Invoke(deletedRows);

                    try
                    {
                        // Checking about the deleted rows.
                        if (deletedRows.Count == deletedRowsNum)
                        {
                            AddStatus("All the Deletes worked perfectly.");
                        }
                        else if (deletedRows.Count > deletedRowsNum)
                        {
                            AddStatus("All the Rows Required didn't get deleted");
                        }
                        else
                        {
                            AddStatus("All the Rows Deleted, have not been sent to the user.");
                        }
                    } catch (Exception er)
                    {
                        // There was an Error
                    }

                    // Reset the Session if another similar query is to be run.
                    dbSess.Dispose();

                    // If the data is to be synced with the server, we will sync it with the server.
                    if (syncable && deletedRowsNum > 0)
                    {
                        // The data is to be synced with the Cloud Server.
                        CloudSync.Sync(this, deletedRows);
                    }
                }
                else
                {
                    // The Delete System Failed somewhere.
                    AddStatus("Err : Delete Failure");
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                AddStatus("Err : " + e.Message + " : " + e.StackTrace);
            }
        }


        // Required Getters and Setters.
        internal JObject GetWhereJObj()
        {
            return wheresJObj;
        }

        internal void SetSyncID(string syncID)
        {
            this.syncID = syncID;
        }

        internal long GetRLSID()
        {
            return rlsID;
        }

        internal short GetRLSType()
        {
            return rlsType;
        }

        internal string GetSyncID()
        {
            return syncID;
        }

        internal string GetTableName()
        {
            return tableName;
        }

        // Get the Status.
        public string GetStatus()
        {
            return status;
        }

        // Get if the System Errored out.
        public bool IsSuccessful()
        {
            return !isError;
        }

        // Here we set the status and the status trace.
        private void AddStatus(string status)
        {
            try
            {
                // Here we Append the status to the status trace and status.
                if (statusTrace == null)
                {
                    statusTrace = new StringBuilder();
                }
                this.status = status;
                statusTrace.Append(status)
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
    }
}

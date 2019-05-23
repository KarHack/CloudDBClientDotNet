using _36E_Business___ERP.helper;
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
    class Insert
    {
        // Implementation.
        /*
            new Insert().Into("Table Name")
                    .PutColumn(new ColumnData("Column Name", "Column Data"))...
                    .SetRoleID(Role ID) OR .SetGroupID(Group ID)
                    .SetSyncable(false) // Optional, Default set to 'True'
                    .Execute(); OR ExecuteAsync();
         */

        // Variables.
        private CloudDB cdb;
        private string tableName;
        private bool syncable = true;
        private StringBuilder columnDataBuilder;
        private StringBuilder columnPlaceHolderBuilder;
        private Dictionary<string, object> bindObjs;
        private string status;
        private long tenantID;
        private short rlsType;
        private long rlsID;
        private bool isError = false;
        private string syncID;
        private StringBuilder statusTrace;
        private long updateTimeI = 0;

        // Static Values.
        internal const string RANDOM = "(ABS(RANDOM())";

        // Callbacks.
        // This will be used to send the insert number to the user if the listener is set.
        public delegate void OnDataInserted(int rowsInserted);
        private OnDataInserted onDataInserted = null;

        // Constructors.
        // Default Constructor.
        // The Cloud DB Constructor.
        public Insert(CloudDB cdb)
        {
            try
            {
                // Lets set the required dsta.
                // Initialize the Cloud DB Service, just in case.
                statusTrace = new StringBuilder();
                // Initialize the Cloud DB Service
                // Lets Connect or create a Cloud DB instance.
                if (cdb.IsSystemUser || cdb.user.IsActive)
                {
                    // Default Constructor.
                    this.cdb = cdb;     // Set the Cloud DB Service.
                    rlsType = 0;
                    tenantID = 0;
                    rlsID = 0;
                    isError = false;
                    // Lets set the Tenant ID.
                    try { tenantID = cdb.user.GetTenantID(); } catch (Exception e) { }

                    AddStatus("User is Authenticated, Insert is Ready.");
                }
                else
                {
                    // The user is not authenticated.
                    isError = false;
                    AddStatus("User not Authenticated");
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                AddStatus("Constructor Err : " + e.StackTrace);
            }
        }

        // The User Token Constructor.
        public Insert(string token)
        {
            try
            {
                // Lets set the required dsta.
                // Initialize the Cloud DB Service, just in case.
                statusTrace = new StringBuilder();
                // Initialize the Cloud DB Service
                // Lets Connect or create a Cloud DB instance.
                CloudDB cdb = new CloudDB(token);
                if (cdb.IsSystemUser || cdb.user.IsActive)
                {
                    // Default Constructor.
                    this.cdb = cdb;     // Set the Cloud DB Service.
                    rlsType = 0;
                    tenantID = 0;
                    rlsID = 0;
                    isError = false;
                    // Lets set the Tenant ID.
                    try { tenantID = cdb.user.GetTenantID(); } catch (Exception e) { }

                    AddStatus("User is Authenticated, Insert is Ready.");
                }
                else
                {
                    // The user is not authenticated.
                    isError = false;
                    AddStatus("User not Authenticated");
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                AddStatus("Constructor Err : " + e.StackTrace);
            }
        }

        // Main Constructor will be used to set the table name.
        public Insert Into(string tableName)
        {
            try
            {
                // Here we will set the table name.
                // Authentication.
                if (this.tableName == null)
                {
                    if (cdb.ValidateCRUD(tableName, CloudDB.CRUD.WRITE) && cdb.user.IsActive)
                    {
                        this.tableName = Regex.Replace(tableName, " ", "");
                        // The user is allowed to write into the table.
                        // Initiate the required variables.
                        columnDataBuilder = new StringBuilder();
                        columnPlaceHolderBuilder = new StringBuilder();
                        bindObjs = new Dictionary<string, object>();
                        syncable = CloudDB.IsSyncable(tableName);
                        AddStatus("Valid CRUD");
                        return this;
                    }
                    else
                    {
                        // The user is not authorized to write into this table.
                        AddStatus("Not Authorized to Write into Table");
                        this.tableName = null;
                        return this;
                    }
                }
                else
                {
                    // The Table name has already been set.
                    return this;
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                this.tableName = null;
                AddStatus("Error with the Cloud DB Validation");
                return this;
            }
        }

        // System Table name of the Insert for only Cloud DB.
        internal Insert IntoSystemInternal(string tableName)
        {
            try
            {
                // Here we will set the table name.
                // Authentication.
                this.tableName = Regex.Replace(tableName, " ", "");
                // The user is allowed to write into the table.
                // Initiate the required variables.
                columnDataBuilder = new StringBuilder();
                columnPlaceHolderBuilder = new StringBuilder();
                bindObjs = new Dictionary<string, object>();
                AddStatus("Name is Valid");
                return this;
            }
            catch (Exception e)
            {
                // There was an Error.
                this.tableName = null;
                AddStatus("Error with the Cloud DB Table Validation");
                return this;
            }
        }

        // System Table name of the Insert
        internal Insert IntoSystem(string tableName)
        {
            try
            {
                // Here we will set the table name.
                // Authentication.
                if (Helper.DBValidation.IsSystemTB(tableName) && cdb.IsSystemUser)
                {
                    this.tableName = Regex.Replace(tableName, " ", "");
                    // The user is allowed to write into the table.
                    // Initiate the required variables.
                    columnDataBuilder = new StringBuilder();
                    columnPlaceHolderBuilder = new StringBuilder();
                    bindObjs = new Dictionary<string, object>();
                    AddStatus("Name is Valid");
                    return this;
                }
                else
                {
                    // The Table name has already been set.
                    AddStatus("Name is Invalid");
                    return this;
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                this.tableName = null;
                AddStatus("Error with the Cloud DB Table Validation");
                return this;
            }
        }

        // Add the Column Data.
        public Insert PutColumn(ColumnData columnData)
        {
            try
            {
                // Here we will Add the column data into the Column SQL & Binders.
                if (bindObjs.ContainsKey(columnData.GetColumn()))
                {
                    // This Column is Already Added.
                    return this;
                }
                else
                {
                    // Only let to proceed if the Column does'nt have the special rows.
                    bool doProceed = true;
                    if (cdb.IsSystemUser)
                    {
                        // The user is the system
                        doProceed = true;
                    }
                    else if (columnData.GetColumn().ElementAt(columnData.GetColumn().Length - 1) == '_')
                    {
                        // This column is not a valid column and must be left only for the system to edit.
                        doProceed = false;
                        AddStatus("User cannot Add " + columnData.GetColumn() + " System Column");
                    }

                    // Proceed if the user is allowed to.
                    if (doProceed)
                    {
                        // This Columns is valid.
                        if (columnDataBuilder.Length > 0)
                        {
                            columnDataBuilder.Append(", ");
                            columnPlaceHolderBuilder.Append(", ");
                        }

                        // Lets check and add the random.
                        if (columnData.Get().ToString().ToUpper() == RANDOM)
                        {
                            // The user wants to add a column to have random value.
                            columnDataBuilder.Append(columnData.GetColumn());
                            columnPlaceHolderBuilder.Append(" (ABS(RANDOM())) ");
                        }
                        else
                        {
                            // The user wants a normal column.
                            columnDataBuilder.Append(columnData.GetColumn());
                            columnPlaceHolderBuilder.Append("@")
                                .Append(columnData.GetColumn());
                            // Put the Bind object.
                            bindObjs.Add(columnData.GetColumn(), columnData.Get());
                        }
                    }

                    return this;
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                AddStatus("Put Column Error : " + e.Message + " : " + e.StackTrace);
                return this;
            }
        }

        private Insert PutColumnInternal(ColumnData columnData)
        {
            try
            {
                // Lets Add the Columns for internal use.
                if (columnDataBuilder.Length > 0)
                {
                    columnDataBuilder.Append(", ");
                    columnPlaceHolderBuilder.Append(", ");
                }

                // Lets check and add the random.
                if (columnData.Get().ToString().ToUpper() == RANDOM)
                {
                    // The user wants to add a column to have random value.
                    columnDataBuilder.Append(columnData.GetColumn());
                    columnPlaceHolderBuilder.Append(" (ABS(RANDOM())) ");
                }
                else
                {
                    // The user wants a normal column.
                    columnDataBuilder.Append(columnData.GetColumn());
                    columnPlaceHolderBuilder.Append("@")
                        .Append(columnData.GetColumn());
                    // Put the Bind object.
                    bindObjs.Add(columnData.GetColumn(), columnData.Get());
                }

                return this;
            }
            catch (Exception e)
            {
                // THere was an Error.
                AddStatus("Put Column Error : " + e.Message + " : " + e.StackTrace);
                return this;
            }
        }

        // Set the update time.
        public Insert SetUpdateTime(long updateTime)
        {
            try
            {
                // Add the Update Time.
                updateTimeI = updateTime;
                return this;
            }
            catch (Exception e)
            {
                // There was an Error.
                return null;
            }
        }

        // Set the Tenant ID.
        internal Insert SetTenantID(long tenantID)
        {
            try
            {
                // Here we will Set the Tenant ID.
                if (cdb.IsSystemUser)
                {
                    // Its a system user.
                    this.tenantID = tenantID;
                }
                else
                {
                    // Its a normal user.
                    this.tenantID = cdb.user.GetTenantID();
                }
                return this;
            }
            catch (Exception er)
            {
                // There was an Error
                return null;
            }
        }

        // Set the Role ID of the Row to be inserted.
        // Has to be a role of the user.
        public Insert SetRoleID(long roleID)
        {
            try
            {
                // Here we will Add the Role ID given by the User to the Statement.
                // Check if the Table is syncable, then only Add the row level security.
                if (CloudDB.IsSyncable(tableName))
                {
                    // Lets validate if the User is allowed to write with this role.
                    if (cdb.ValidateRoleRLS(roleID, CloudDB.CRUD.WRITE) || cdb.IsSystemUser)
                    {
                        // The user is allowed to write with this role.
                        rlsType = C.RLS.ROW_RLS;
                        rlsID = roleID;
                        AddStatus("User is Authorized to Write in this Role");
                        return this;
                    }
                    else
                    {
                        // The user is not allowed to write with this role.
                        rlsType = C.RLS.NO_RLS;
                        rlsID = 0;
                        AddStatus("User is NOT Authorized to Write in this Role");
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
            catch (Exception e)
            {
                // There was an Error.
                rlsType = C.RLS.NO_RLS;
                rlsID = 0;
                AddStatus("Error : " + e.Message);
                isError = true;
                return this;
            }
        }

        // Set the Group ID of the Row to be inserted.
        // Has to be a Group of the user.
        public Insert SetGroupID(long groupID)
        {
            try
            {
                // Here we will Add the Group ID given by the User to the Statement.
                // Check if the Table is syncable, then only Add the row level security.
                if (CloudDB.IsSyncable(tableName))
                {
                    // Lets validate if the User is allowed to write with this role.
                    if (cdb.ValidateGroupRLS(groupID, CloudDB.CRUD.WRITE) || cdb.IsSystemUser)
                    {
                        // The user is allowed to write with this role.
                        rlsType = C.RLS.GROUP_RLS;
                        rlsID = groupID;
                        AddStatus("User is Authorized to Write in this Group");
                        return this;
                    }
                    else
                    {
                        // The user is not allowed to write with this role.
                        rlsType = C.RLS.NO_RLS;
                        rlsID = 0;
                        isError = true;
                        AddStatus("User is NOT Authorized to Write in this Group");
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
            catch (Exception e)
            {
                // There was an Error.
                rlsType = C.RLS.NO_RLS;
                rlsID = 0;
                AddStatus("Error : " + e.Message);
                isError = true;
                return this;
            }
        }

        // Let the user to suggest which rls to add to in one method only.
        public Insert SetRLSID(short rowLevelSecurityType, long rowLevelSecurityID)
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
                            if (cdb.ValidateRoleRLS(rowLevelSecurityID, CloudDB.CRUD.WRITE))
                            {
                                // The user is allowed to write with this role.
                                rlsType = C.RLS.ROW_RLS;
                                rlsID = rowLevelSecurityID;
                                AddStatus("User is Authorized to Write in this Role");
                                return this;
                            }
                            else
                            {
                                // The user is not allowed to write with this role.
                                rlsType = C.RLS.NO_RLS;
                                rlsID = 0;
                                AddStatus("User is NOT Authorized to Write in this Role");
                                isError = true;
                                return this;
                            }
                        // Its a Group Based RLS.
                        case 2:
                            // Lets validate if the User is allowed to write with this role.
                            if (cdb.ValidateGroupRLS(rowLevelSecurityID, CloudDB.CRUD.WRITE))
                            {
                                // The user is allowed to write with this role.
                                rlsType = C.RLS.GROUP_RLS;
                                rlsID = rowLevelSecurityID;
                                AddStatus("User is Authorized to Write in this Group");
                                return this;
                            }
                            else
                            {
                                // The user is not allowed to write with this role.
                                rlsType = C.RLS.NO_RLS;
                                rlsID = 0;
                                isError = true;
                                AddStatus("User is NOT Authorized to Write in this Group");
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
        public Insert SetRowLevelSecurityID(short rowLevelSecurityType, long rowLevelSecurityID)
        {
            return SetRLSID(rowLevelSecurityType, rowLevelSecurityID);
        }

        // Another Mimic of the SetRLSID Method.
        public Insert SetRLSID(RowLevelSecurity rowLevelSecurity)
        {
            return SetRLSID(rowLevelSecurity.Type, rowLevelSecurity.ID);
        }

        // Here we will set syncable to false, if the user wants to.
        public Insert SetSyncable(bool syncable)
        {
            this.syncable = syncable;
            return this;
        }

        // Here we will get the user related to this insert.
        internal User GetUser()
        {
            try
            {
                // Here we will return the user related to this insert.
                return cdb.IsSystemUser ? null : cdb.user;
            }
            catch (Exception e)
            {
                // There was an Error
                return null;
            }
        }

        // Set the Callback Listeners for the User.
        public Insert SetOnDataInserted(OnDataInserted onDataInserted)
        {
            this.onDataInserted = onDataInserted;
            return this;
        }

        // Here we generate the SQL Statement for the User.
        public string GetSQL()
        {
            try
            {
                // Here we generate the SQL Statement.
                // Attach the Main Select statement.
                StringBuilder sqlBuilder = new StringBuilder();

                // Validate if the User can procceed.
                if (IsSuccessful())
                {
                    // There are no errors in the Insert.
                    // Generate the Insert Query.
                    sqlBuilder.Append("INSERT INTO ")
                        // Add the Table Name.
                        .Append(tableName)
                        .Append(" (")
                        // Add the Column Names to the Insert Statement.
                        .Append(columnDataBuilder)
                        .Append(")")
                        .Append(" VALUES (")
                        // Add the Place holders for the Prepared Statements.
                        .Append(columnPlaceHolderBuilder)
                        .Append(")");
                }
                else
                {
                    // The user is not allowed to write.
                    sqlBuilder.Append("Error in Insert");
                }
                return sqlBuilder.ToString();

            }
            catch (Exception e)
            {
                // There was an Error.
                return e.Message;
            }
        }

        // Lets now execute the statement.
        // This will Execute in Synchronous Mode.
        public int Execute()
        {
            try
            {
                // Here we will execute the query.
                return InternalExecutor();
            }
            catch (Exception e)
            {
                // There was an Error.
                return -1;
            }
        }

        // This will Execute in A-Synchronous Mode.
        public void ExecuteAsync()
        {
            try
            {
                // Here we will execute the query.
                // Create the thread.
                Thread executionThread = new Thread(() =>
                {
                    try
                    {
                        // Now we will run the internal executor.
                        InternalExecutor();
                    }
                    catch (Exception e)
                    {
                        // There was an Error.
                    }
                });
                // Start the thread.
                executionThread.Start();
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // Here we will run an internal executor to run the actual execution of the statement.
        private int InternalExecutor()
        {
            try
            {
                // Here we will Execute the Statement, and bind the Params.
                // Lets create the Statement.
                if (IsSuccessful())
                {
                    // Reset the Session So if Any uncompleted Call does'nt interupt this call.
                    SQLiteCommand dbSess = cdb.GetSession();
                    dbSess.Reset();

                    // Check if the table is a syncable table.
                    if (CloudDB.IsSyncable(tableName))
                    {
                        // The table is a syncable table.
                        // Lets Add the RLS system.
                        if (rlsID == 0 || rlsType == 0)
                        {
                            // The table is a syncable table, but the RLS Values are not provided.
                            // So we will not let it complete the process.
                            isError = true;
                            AddStatus("User NOT Authenticated to Write in this RLS");
                        }
                        else
                        {
                            // The user has provided the RLS Data.
                            PutColumnInternal(new ColumnData("rls_id_", rlsID));
                            PutColumnInternal(new ColumnData("rls_type_", rlsType));
                        }

                        // Now lets generate the Sync ID.
                        if (GetSyncID() == null || GetSyncID().Trim().Length != 36)
                        {
                            // The sync id has not been set.
                            // Lets set it.
                            SetSyncID(Guid.NewGuid().ToString());
                        }
                        PutColumnInternal(new ColumnData("sync_id_", GetSyncID()));

                        // Lets set the Update Time.
                        try
                        {
                            if (updateTimeI == 0)
                            {
                                // Lets Create an Update Time.
                                updateTimeI = Helper.CurrentTimeMillis();
                            }
                            PutColumnInternal(new ColumnData("update_time_", updateTimeI));
                        }
                        catch (Exception er)
                        {
                            // There was an Error.
                        }
                    }
                    else
                    {
                        // Lets set the Other Data Points if they are given explicitly.
                        if (updateTimeI != 0)
                        {
                            PutColumnInternal(new ColumnData("update_time_", updateTimeI));
                        }
                    }

                    // Set the Tenant ID.
                    if (CloudDB.IsMultiTenant(tableName))
                    {
                        // The table is multi-tenant.
                        PutColumnInternal(new ColumnData("tenant_id_", tenantID));
                    }

                    // The Insert Statement Generating was succesful, we can run the Statement.
                    string sql = GetSQL();
                    breadCrumb.BC.Debug("INSERT SQL : " + sql);
                    dbSess.CommandText = sql;

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
                    int inserted = -1;

                    // Lets Execute the Query.
                    // The user is not asking to create a reader, just to insert the value.
                    inserted = dbSess.ExecuteNonQuery();

                    // Send to the Receiver if the user has set a insert result listener.
                    onDataInserted?.Invoke(inserted);

                    // If the data is to be synced with the server, we will sync it with the server.
                    if (syncable)
                    {
                        // The data is to be synced with the Cloud Server.
                        CloudSync.Sync(this);
                    }
                    return inserted;
                }
                else
                {
                    // The Insert Statement Generation was not successful.
                    return -1;
                }
            }
            catch (Exception e)
            {
                // THere was an Error.
                Console.WriteLine("Err : " + e.Message + " : " + e.StackTrace);
                AddStatus("Err : " + e.Message + " : " + e.StackTrace);
                return -1;
            }
        }

        // The Required Getters and Setters in the System
        internal Insert SetSyncID(string syncID)
        {
            this.syncID = syncID;
            return this;
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

        internal Dictionary<string, object> GetColumnData()
        {
            return bindObjs;
        }

        internal string GetTableName()
        {
            return tableName;
        }

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

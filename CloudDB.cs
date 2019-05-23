using _36E.waiter;
using _36E_Business___ERP.cloudPush;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading;

namespace _36E_Business___ERP.cloudDB
{
    /* 
     * 
     * This class will be used to Handle the CRUD Operations into the System.
     * This will also take care of the Connections with the Cloud Server by Handing of the Task to Cloud Sync.
     * It will allow users to set data change listeners.
     * It will also take care of Network changes like Internet availability and sync with the server.
     * This will also maintain the connection with the offline database.
     * 
     */
    class CloudDB
    {
        // Variables
        private string Status { get; set; }
        private StringBuilder StatusTrace { get; set; }
        private DBConnector dbConn; // This will be used for each new Instance.
        internal bool IsSystemUser { get; } = false;
        internal User user { get; set; }

        // Enums.
        internal enum QueryType
        {
            SELECT, INSERT, UPDATE, DELETE
        }

        // Static Variables.
        private static Dictionary<string, short[]> tableMeta;   // 0 -> Syncable, 1 -> Offline, 2 -> Multi-Tenant
        private static string StaticStatus { get; set; }
        private static StringBuilder StaticStatusTrace { get; set; }
        private static int numberOfUserTablesToBeSynced;

        // Static Constants.
        public enum CRUD
        {
            READ, WRITE, UPDATE, DELETE
        }

        public enum SyncStatus
        {
            OUT_OF_SYNC, SYSTEM_TABLES, USER_TABLES, SYNCING, SYNCED, GENERAL_SYNC
        }

        public static class TableTenancyType
        {
            public const string NONE = "NONE";
            public const string DATABASE = "DATABASE";
            public const string TENANT = "TENANT";
            public const string USER = "USER";
            public const string RLS = "RLS";
        }

        // Callbacks.
        public delegate void OnSyncStatusChanged(SyncStatus syncStatus, bool completed);
        private static OnSyncStatusChanged onSyncStatusChanged = null;

        // This will maintain all the types of all the column data.
        public static class DataType
        {

            public const short SHORT = 1000;
            public const short INTEGER = 1001;
            public const short LONG = 1002;
            public const short FLOAT = 1003;
            public const short DOUBLE = 1004;
            public const short BOOLEAN = 1005;
            public const short STRING = 1006;
            public const short BYTE = 1007;
            public const short BYTE_ARR = 1008;
        }

        // The Inialization Method.
        // This should be started everytime the application is loaded.
        // It will be called when a cloud db object is initialized.
        // It is better if init is called on load, so it can setup the required connections 
        // And all other applications related to Cloud DB.
        public static bool Init()
        {
            try
            {
                // Lets initialize the Cloud DB Service.
                // The Cloud DB was not initialize it.
                // Lets set it up.
                // Setup the Local Database Connection.
                DBConnector.Init();


                // Setup the Service
                // Setup the Tables of the Database.
                SetupTables();

                // Setup the Table Meta.
                // Lets Fill it up.
                SetupTableMeta();

                // Lets Initialize the Cloud Push Service & Other Similar Services.
                try
                {
                    // Lets initialize the Cloud Push service.
                    // We will initialize according to the Users Logged in.
                    new Select(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                        .FromSystem("users_")
                        .SetOnDataResult((List<Row> rows) =>
                        {
                            try
                            {
                                // Initialize the Cloud Push Service according to the users.
                                for (int i = 0; i < rows.Count; i++)
                                {
                                    try
                                    {
                                        // Get the data and start the service.
                                        Row row = rows[i];
                                        CloudSync.Init(row.GetString("push_token"));    // Initialize the Service.
                                    }
                                    catch (Exception e)
                                    {
                                        // There was an Error.
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                // There was an Error.
                            }
                        })
                        .Execute();
                }
                catch (Exception e)
                {
                    // There was an Error.
                }

                // Lets Deliver the Messages that were not previously due to errors.
                try
                {
                    // Here we will handle the waiter calls and make sure all data goes to the end user.
                    PushManager.SetOnNetworkChangedListener((PushManager.Network networkEvent) =>
                    {
                        try
                        {
                            // There is an Event with the Internet Connection.
                            // Check with Waiter Manager, if any calls are running.
                            if (networkEvent == PushManager.Network.CONNECTED)
                            {
                                int currentRunningCalls = WaiterManager.RunningRequests();
                                new Select(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                    .FromSystem("sync_offline_")
                                    .SetOnDataResult((List<Row> rows) =>
                                    {
                                        try
                                        {
                                            // Check number of Data Points to be synced.
                                            if (rows.Count == currentRunningCalls)
                                            {
                                                // All the Sync Calls are getting taken care of.
                                                // We don't have to do anything.
                                            }
                                            else
                                            {
                                                // Lets Check for the Anomolies.
                                                if (rows.Count > 0)
                                                {
                                                    // There is data to be synced.
                                                    // Now lets Check the number of data points that are being synced.
                                                    if (currentRunningCalls > 0)
                                                    {
                                                        // There are few rows being synced.
                                                        Dictionary<string, Waiter> waiters = WaiterManager.GetRunningRequests();
                                                        for (int rowIndex = rows.Count; rowIndex > 0; rowIndex--)
                                                        {
                                                            try
                                                            {
                                                                string dataToCompare = rows[rowIndex].GetString("id")
                                                                        + rows[rowIndex].GetBigInt("update_time_");
                                                                foreach (string paramName in waiters.Keys)
                                                                {
                                                                    try
                                                                    {
                                                                        // Here we will Check if the Running Call is same as the row that is not yet synced.
                                                                        if (dataToCompare.Equals(waiters[paramName].GetUID()))
                                                                        {
                                                                            // The call exists and is running.
                                                                            // Lets remove it from the rows.
                                                                            rows.RemoveAt(rowIndex);
                                                                            break;
                                                                        }
                                                                    }
                                                                    catch (Exception e)
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
                                                        // All the rows that are being synced are removed.
                                                        // Lets now start the syncing of the offline data.
                                                        PushDataToServer(rows);
                                                    }
                                                    else
                                                    {
                                                        // There are no rows being synced currently.
                                                        // Lets now start the syncing of the offline data.
                                                        PushDataToServer(rows);
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            // There was an Error.
                                        }
                                    })
                                    .ExecuteAsync();
                            }
                        }
                        catch (Exception e)
                        {
                            // There was an Error.
                        }
                    });
                }
                catch (Exception e)
                {
                    // There was an Error.
                }
                return true;
            }
            catch (Exception e)
            {
                // There was an Error.
                return false;
            }
        }

        // Here we will push all the rows given to the server.
        private static void PushDataToServer(List<Row> rows)
        {
            try
            {
                // Lets Get the Data as a JSON Object.
                // Now we will run this on a separate thread.
                new Thread(new ThreadStart(() =>
                {
                    try
                    {
                        // Here we will start the uploading to the server.
                        // We will keep it one at a time in sync mode, so that the calls don't go out of order.
                        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                        {
                            // There are rows to be synced.
                            Row row = rows[rowIndex];  // Get the Row to be synced.

                            // Now lets Get the Data from the Row.
                            JObject dataJObj = JObject.Parse(row.GetString("data"));
                            // Get the CUD type of the API call.
                            string cudType = row.GetString("crud_type");
                            if (cudType == "INSERT")
                            {
                                // Here we will Create the Waiter Calls and Send the Data to the server.
                                new Waiter()
                                    .Http()
                                    .Url(C.CLOUD_DB_IP_ADDRESS)
                                    .Endpoint("CloudDB/Table/Data")
                                    .Method(Waiter.CallMethod.POST)
                                    .AddParam(new ParamData("columns").Put(JObject.Parse(dataJObj["columns"].ToString())))
                                    .AddParam(new ParamData("rls_id_").Put(dataJObj["rls_id_"].ToString()))
                                    .AddParam(new ParamData("rls_type_").Put(dataJObj["rls_type_"].ToString()))
                                    .AddParam(new ParamData("table_name").Put(row.GetString("table_name")))
                                    .AddParam(new ParamData("token").Put(row.GetString("token")))
                                    .AddParam(new ParamData("sync_id").Put(row.GetString("id")))
                                    .AddParam(new ParamData("v").Put(C.VERSION_0_1))
                                    .AddParam(new ParamData("update_time_").Put(row.GetBigInt("update_time_")))
                                    .SetUID(row.GetString("id") + row.GetBigInt("update_time_"))
                                    // Set up the Callback.
                                    .SetOnResponse((bool isSuccess, string response) =>
                                    {
                                        try
                                        {
                                            // Here we will perform actions on the response.
                                            // Here we will Delete the Data from the Sync System Table.
                                            // Lets validate the response.
                                            JObject respJObj = JObject.Parse(response);
                                            if (respJObj.ContainsKey("success"))
                                            {
                                                // The data was properly inserted.
                                                new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                                            .FromSystem("sync_offline_")
                                                            .AddWhere(new Where("id", Where.Type.EQUAL, row.GetString("id")))
                                                            .AddWhere(new Where("update_time_", Where.Type.EQUAL,
                                                                    row.GetBigInt("update_time_")))
                                                            .Execute();
                                            }
                                            else
                                            {
                                                // The data was not properly added.
                                                try
                                                {
                                                    // Get the Error and check it.
                                                    if (isSuccess)
                                                    {
                                                        // The API Ran Fully.
                                                        string errStr = respJObj["error"].ToString().ToLower();
                                                        if (//errStr.Contains("unauthorized err : ") ||
                                                            errStr.Contains("insert error") ||
                                                            errStr.Contains("duplicate Key"))
                                                        {
                                                            // The Error was due to a reason that will always happen
                                                            // So we will delete from the database to be synced.
                                                            // The data was properly inserted.
                                                            new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                                                        .FromSystem("sync_offline_")
                                                                        .AddWhere(new Where("id", Where.Type.EQUAL, row.GetString("id")))
                                                                        .AddWhere(new Where("update_time_", Where.Type.EQUAL,
                                                                                row.GetBigInt("update_time_")))
                                                                        .Execute();
                                                        }
                                                    }
                                                }
                                                catch (Exception e)
                                                {
                                                    // There was an Error.
                                                }
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            // Here we have an Error.
                                            // Lets handle the error.
                                            // The data was not properly added.
                                            try
                                            {
                                                // Get the Error and check it.
                                                if (isSuccess)
                                                {
                                                    // The API Ran Fully.
                                                    response = response.ToLower();
                                                    if (response.Contains("User Not Authenticated".ToLower())
                                                        || response.Contains("Unauthorized Err : ".ToLower())
                                                        || response.Contains("Insert Error".ToLower()))
                                                    {
                                                        // The Error was due to a reason that will always happen
                                                        // So we will delete from the database to be synced.
                                                        // The data was properly inserted.
                                                        new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                                            .FromSystem("sync_offline_")
                                                            .AddWhere(new Where("id", Where.Type.EQUAL, row.GetString("id")))
                                                            .AddWhere(new Where("update_time_", Where.Type.EQUAL, row.GetString("update_time_")))
                                                            .Execute();
                                                    }
                                                }
                                            }
                                            catch (Exception er)
                                            {
                                                // There was an Error.
                                            }
                                        }
                                    })
                                    .Execute();
                            }
                            else if (cudType == "UPDATE")
                            {
                                // Here we will Create the Waiter Calls to Send the Update data to the server.
                                new Waiter()
                                    .Http()
                                    .Url(C.CLOUD_DB_IP_ADDRESS)
                                    .Endpoint("CloudDB/Table/Data")
                                    .Method(Waiter.CallMethod.PUT)
                                    .AddParam(new ParamData("columns").Put(JObject.Parse(dataJObj["columns"].ToString())))
                                    .AddParam(new ParamData("wheres").Put(JObject.Parse(dataJObj["wheres"].ToString())))
                                    .AddParam(new ParamData("table_name").Put(row.GetString("table_name")))
                                    .AddParam(new ParamData("token").Put(row.GetString("token")))
                                    .AddParam(new ParamData("v").Put(C.VERSION_0_1))
                                    .AddParam(new ParamData("update_time_").Put(row.GetBigInt("update_time_")))
                                    .SetUID(row.GetString("id") + row.GetBigInt("update_time_"))
                                    // Set up the Callback.
                                    .SetOnResponse((bool isSuccess, string response) =>
                                    {
                                        try
                                        {
                                            // Here we will perform actions on the response.
                                            // Here we will Delete the Data from the Sync System Table.
                                            // Lets validate the response.
                                            JObject respJObj = JObject.Parse(response);
                                            if (respJObj.ContainsKey("success"))
                                            {
                                                // The data was properly inserted.
                                                new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                                            .FromSystem("sync_offline_")
                                                            .AddWhere(new Where("id", Where.Type.EQUAL, row.GetString("id")))
                                                            .AddWhere(new Where("update_time_", Where.Type.EQUAL,
                                                                    row.GetBigInt("update_time_")))
                                                            .ExecuteAsync();
                                            }
                                            else
                                            {
                                                // The data was not properly added.
                                                // Get the Error and check it.
                                                if (isSuccess)
                                                {
                                                    // The API Ran Fully.
                                                    string errStr = respJObj["error"].ToString().ToLower();
                                                    if (errStr.Contains("unauthorized err : ")
                                                            || errStr.Contains("insert error")
                                                            || errStr.Contains("duplicate Key"))
                                                    {
                                                        // The Error was due to a reason that will always happen
                                                        // So we will delete from the database to be synced.
                                                        // The data was properly inserted.
                                                        new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                                                    .FromSystem("sync_offline_")
                                                                    .AddWhere(new Where("id", Where.Type.EQUAL, row.GetString("id")))
                                                                    .AddWhere(new Where("update_time_", Where.Type.EQUAL,
                                                                            row.GetBigInt("update_time_")))
                                                                    .ExecuteAsync();
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            // Here we have an Error.
                                            // We have to Properly handle the Error.
                                            // The data was not properly added.
                                            try
                                            {
                                                // Get the Error and check it.
                                                if (isSuccess)
                                                {
                                                    // The API Ran Fully.
                                                    response = response.ToLower();
                                                    if (response.Contains("User Not Authenticated".ToLower())
                                                        || response.Contains("Unauthorized Err : ".ToLower())
                                                        || response.Contains("Insert Error".ToLower()))
                                                    {
                                                        // The Error was due to a reason that will always happen
                                                        // So we will delete from the database to be synced.
                                                        // The data was properly inserted.
                                                        new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                                            .FromSystem("sync_offline_")
                                                            .AddWhere(new Where("id", Where.Type.EQUAL, row.GetString("id")))
                                                            .AddWhere(new Where("update_time_", Where.Type.EQUAL, row.GetString("update_time_")))
                                                            .Execute();
                                                    }
                                                }
                                            }
                                            catch (Exception er)
                                            {
                                                // There was an Error.
                                            }
                                        }
                                    })
                                    .Execute();

                            }
                            else if (cudType == "DELETE")
                            {
                                // Here we will Create the Waiter Calls to Send the Delete data to the server.
                                new Waiter()
                                    .Url(C.CLOUD_DB_IP_ADDRESS)
                                    .Endpoint("CloudDB/Table/Data")
                                    .Method(Waiter.CallMethod.DELETE)
                                    .AddParam(new ParamData("wheres").Put(JObject.Parse(dataJObj["wheres"].ToString())))
                                    .AddParam(new ParamData("table_name").Put(row.GetString("table_name")))
                                    .AddParam(new ParamData("token").Put(row.GetString("token")))
                                    .AddParam(new ParamData("v").Put(C.VERSION_0_1))
                                    .AddParam(new ParamData("delete_time_").Put(row.GetBigInt("update_time_")))
                                    .SetUID(row.GetString("id") + row.GetBigInt("update_time_"))
                                    // Set up the Callback.
                                    .SetOnResponse((bool isSuccess, string response) =>
                                    {
                                        try
                                        {
                                            // Here we will perform actions on the response.
                                            // Here we will Delete the Data from the Sync System Table.
                                            // Lets validate the response.
                                            JObject respJObj = JObject.Parse(response);
                                            if (respJObj.ContainsKey("success"))
                                            {
                                                // The data was properly inserted.
                                                new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                                            .FromSystem("sync_offline_")
                                                            .AddWhere(new Where("id", Where.Type.EQUAL, row.GetString("id")))
                                                            .AddWhere(new Where("update_time_", Where.Type.EQUAL,
                                                                    row.GetBigInt("update_time_")))
                                                            .ExecuteAsync();
                                            }
                                            else
                                            {
                                                // The data was not properly added.
                                                // Get the Error and check it.
                                                if (isSuccess)
                                                {
                                                    // The API Ran Fully.
                                                    string errStr = respJObj["error"].ToString().ToLower();
                                                    if (errStr.Contains("unauthorized err : ")
                                                            || errStr.Contains("insert error")
                                                            || errStr.Contains("duplicate Key"))
                                                    {
                                                        // The Error was due to a reason that will always happen
                                                        // So we will delete from the database to be synced.
                                                        // The data was properly inserted.
                                                        new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                                                    .FromSystem("sync_offline_")
                                                                    .AddWhere(new Where("id", Where.Type.EQUAL, row.GetString("id")))
                                                                    .AddWhere(new Where("update_time_", Where.Type.EQUAL,
                                                                            row.GetBigInt("update_time_")))
                                                                    .ExecuteAsync();
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            // Here we have an Error.
                                            // We have to Properly handle the Error.
                                            // The data was not properly added.
                                            try
                                            {
                                                // Get the Error and check it.
                                                if (isSuccess)
                                                {
                                                    // The API Ran Fully.
                                                    response = response.ToLower();
                                                    if (response.Contains("User Not Authenticated".ToLower())
                                                        || response.Contains("Unauthorized Err : ".ToLower())
                                                        || response.Contains("Insert Error".ToLower()))
                                                    {
                                                        // The Error was due to a reason that will always happen
                                                        // So we will delete from the database to be synced.
                                                        // The data was properly inserted.
                                                        new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                                            .FromSystem("sync_offline_")
                                                            .AddWhere(new Where("id", Where.Type.EQUAL, row.GetString("id")))
                                                            .AddWhere(new Where("update_time_", Where.Type.EQUAL, row.GetString("update_time_")))
                                                            .Execute();
                                                    }
                                                }
                                            }
                                            catch (Exception er)
                                            {
                                                // There was an Error.
                                            }
                                        }
                                    })
                                    .Execute();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        // There was an Error.
                        MainWindow.Message("Sync Err : " + e.Message + " : " + e.StackTrace);
                    }
                }))
                .Start();
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // Here we will allow the user to setup the token of the user for the registeration process with cloud db service.
        public static void Register(string token)
        {
            try
            {
                // Here we will try to register with the cloud db server.
                new Waiter().Url(C.CLOUD_DB_IP_ADDRESS)
                    .Endpoint("CloudDB/User/Sync/Auth")
                    .AddParam(new ParamData("v").Put(0.1))
                    .AddParam(new ParamData("token").Put(token))
                    .Method(Waiter.CallMethod.GET)
                    .SetOnResponse((bool isSuccess, string response) =>
                    {
                        try
                        {
                            // Here we will process the response and determine the authentication of the user.
                            if (isSuccess)
                            {
                                // The response was received without error.
                                JObject respJObj = JObject.Parse(response);
                                // Extract the data.
                                if (respJObj.ContainsKey("success"))
                                {
                                    // Here we will get the data of the user.
                                    // Extract the required data. 
                                    JObject succJObj = (JObject)respJObj.GetValue("success");

                                    // Store in the database.
                                    // Create the Statement.
                                    Insert inser = new Insert(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                            .IntoSystem("users_")
                                            .PutColumn(new ColumnData("user_id").Put(succJObj["user_id"].ToString()))
                                            .PutColumn(new ColumnData("device_uid").Put(succJObj["device_uid"].ToString()))
                                            .PutColumn(new ColumnData("token").Put(token))
                                            .PutColumn(new ColumnData("push_token").Put(succJObj["push_token"].ToString()))
                                            .PutColumn(new ColumnData("is_active").Put(bool.Parse(succJObj["is_active"].ToString())))
                                            .PutColumn(new ColumnData("tenant_id").Put(long.Parse(succJObj["tenant_id"].ToString())))
                                            .PutColumn(new ColumnData("database_id").Put(succJObj["database_id"].ToString()))
                                            .SetSyncable(false);
                                    inser.Execute();

                                    // Initialize the Cloud DB Sync.
                                    CloudSync.Init(succJObj["push_token"].ToString());
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            // There was an Error
                        }
                    })
                    .Execute();
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // Default Constructor.
        private CloudDB()
        {
            // We do not need this constructor.
        }

        // This will be the main constructor of this class.
        internal CloudDB(string token)
        {
            try
            {
                if (token == C.SYSTEM_USER_DB_PASSCODE)
                {
                    // The user is a system user.
                    IsSystemUser = true;
                    user = null;
                    // Create the DB Connection.
                    dbConn = new DBConnector();
                }
                // Now lets validate if the user is authenticated.
                else if (AuthenticateUser(token))
                {
                    // Here we will Initialize the Required Variables.
                    // Lets setup the Required Constructs.
                    // Create the DB Connection.
                    dbConn = new DBConnector();
                    // Create & Add the User Data.
                    new Select(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                        .FromSystem("users_")
                        .AddWhere(new Where("token", Where.Type.EQUAL, token))
                        .SetOnDataResult((List<Row> users) =>
                        {
                            try
                            {
                                if (users.Count > 0)
                                {
                                    // There are users.
                                    Row userRw = users[0];
                                    // Get the Device Data.
                                    Device device = new Device(userRw.GetString("device_uid"), userRw.GetString("token"));
                                    device.SetPushToken(userRw.GetString("push_token"));    // Set the Push token.
                                    // Get the User data.
                                    user = new User(userRw.GetString("user_id"), userRw.GetString("token"), device,
                                                userRw.GetString("database_id"), userRw.GetBigInt("tenant_id"));
                                    user.SetActive(userRw.GetBoolean("is_active"));
                                }
                                else
                                {
                                    // There are no Users.
                                }
                            }
                            catch (Exception e)
                            {
                                // There was an Error.
                            }
                        })
                        .Execute();
                }
                else
                {
                    // The user is not Authenticated.
                    user = null;
                    IsSystemUser = false;
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                AddStatus("Constructor Err : " + e.Message);
            }
        }

        // Authenticate the user with the Offline Table.
        internal static bool AuthenticateUser(string token)
        {
            try
            {
                // Lets authenticate the user.
                bool isAuthenticated = false;
                new Select(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                    .FromSystem("users_")
                    .AddWhere(new Where("token", Where.Type.EQUAL, token))
                    .SetOnDataResult((List<Row> users) =>
                    {
                        try
                        {
                            if (users.Count > 0)
                            {
                                // There are users.
                                Row userRw = users[0];
                                isAuthenticated = userRw.GetBoolean("is_active");
                            }
                            else
                            {
                                // There are no Users.
                                isAuthenticated = false;
                            }
                        }
                        catch (Exception e)
                        {
                            // There was an Error.
                            isAuthenticated = false;
                        }
                    })
                    .Execute();
                return isAuthenticated;
            }
            catch (Exception e)
            {
                // There was an Error.
                AddStaticStatus("Authentication Err : " + e.Message);
                return false;
            }
        }

        // Get all the Tables Meta Data and Store in Cache.
        internal static void SetupTableMeta()
        {
            try
            {
                // Here we setup the table meta cache.
                // Get the Table Meta Data from the Table.
                new Select(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                    .FromSystem("tables_")
                    .SetOnDataResult((List<Row> rows) =>
                    {
                        try
                        {
                            // Here we will Extract the data from the rows and fill the Caching layer.
                            if (rows.Count > 0)
                            {
                                // There are table meta data to be extracted.
                                tableMeta = new Dictionary<string, short[]>();  // Just initializing in case.
                                foreach (Row row in rows)
                                {
                                    try
                                    {
                                        // Lets extract the row data.
                                        short[] tableMetaV = new short[3];
                                        tableMetaV[0] = row.GetBoolean("syncable") ? (short)1 : (short)0;
                                        tableMetaV[1] = row.GetBoolean("offline_only") ? (short)1 : (short)0;
                                        tableMetaV[2] = row.GetBoolean("multi_tenant") ? (short)1 : (short)0;
                                        // Add the values into the table meta cache.
                                        tableMeta.Add(row.GetString("id"), tableMetaV);
                                    }
                                    catch (Exception e)
                                    {
                                        // There was an Error.
                                    }
                                }
                            }
                            else
                            {
                                // There are no rows to extract.
                            }
                        }
                        catch (Exception e)
                        {
                            // There was an Error.
                        }
                    })
                    .Execute();
            }
            catch (Exception e)
            {
                // There was an Error.
                //AddStatus("Table Meta Setup Err : " + e.Message);
            }
        }

        // Here we create the required system tables.
        private static void SetupTables()
        {
            try
            {
                // Create the system tables.
                // Let check if the Updates table is created.
                new Select(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                    .FromSystem("sqlite_master")
                    .AddWhere(new Where("name", Where.Type.EQUAL, "updates_"))
                    .SetOnDataResult((List<Row> rows) =>
                    {
                        try
                        {
                            // Here we will Get the List of Rows from the Query.
                            if (rows.Count > 0)
                            {
                                // We found update table exists in the tables.
                                // Get the Update Information and the Software Version of the Current Build.
                                new Select(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                        .FromSystem("updates_")
                                        .SetOnDataResult((List<Row> updateRows) =>
                                        {
                                            try
                                            {
                                                // Here we will try to get the data from the update row.
                                                // Check if there are any update versions in the system.
                                                if (updateRows.Count > 0)
                                                {
                                                    // There are Update Details, now we will extract them.
                                                    // Lets Read the Database Version.
                                                    int dbVersion = updateRows[0].GetInt("database_version");
                                                    // Lets setup the database according to the current version
                                                    bool setupDone = SetupTables(dbVersion + 1);
                                                    //AddStaticStatus("Table Setup DONE : " + setupDone);
                                                }
                                                else
                                                {
                                                    // There are no updates currently done.
                                                    // This error is probably very weird.
                                                    // Lets just be safe and go through the process from the begining.
                                                    bool setupDone = SetupTables(1);
                                                    //AddStaticStatus("Table Setup DONE : " + setupDone);
                                                }
                                            }
                                            catch (Exception e)
                                            {
                                                // There was an Error.
                                            }
                                        }).Execute();
                            }
                            else
                            {
                                // We didn't find any update details.
                                bool setupDone = SetupTables(1);
                                //AddStaticStatus("Table Setup DONE : " + setupDone);
                            }

                        }
                        catch (Exception e)
                        {
                            // There was an Error.
                        }
                    })
                    .Execute();
            }
            catch (Exception e)
            {
                // THere was an Error.
                // Lets just create the tables of all the versions.
                bool setupDone = SetupTables(1);
                //AddStaticStatus("Table Setup DONE : " + setupDone);
            }
        }

        // Lets create the First Version of all the tables.
        private static bool SetupTables(int startVersion)
        {
            try
            {
                // Here we will create the required tables.
                // Lets Segregate all the Create Queries according to version.
                if (startVersion == 1)
                {
                    AddStaticStatus("Starting Setup : " + startVersion);
                    bool allTablesCreated = true;
                    allTablesCreated = TB.SetupSystemTable(1);

                    // All the System Tables required for this version are created.
                    // Lets now create all the 36E Business Related Tables.
                    allTablesCreated = TB.SetupTablesV1();

                    // Insert into the Update table.
                    // This is required for all the next updates for the system.
                    new Insert(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                        .IntoSystem("updates_")
                        .PutColumn(new ColumnData("id").Put(1))
                        .PutColumn(new ColumnData("software_version").Put(0.1))
                        .PutColumn(new ColumnData("software_version_name").Put("Development Version"))
                        .PutColumn(new ColumnData("database_version").Put(1))
                        .PutColumn(new ColumnData("update_release_date").Put("01/03/2018"))
                        .SetSyncable(false)
                        .SetOnDataInserted((int inserted) =>
                        {
                            allTablesCreated = inserted > 0;
                        })
                        .Execute();

                    // Lets create the Next Version of tables and alters.
                    SetupTables(startVersion + 1);
                    //AddStaticStatus("Finished Setup of All the Tables. " + allTablesCreated);
                    return allTablesCreated;
                }
                else
                {
                    // Not a valid version.
                    return false;
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                AddStaticStatus("Setup Er : " + e.Message);
                return false;
            }
        }

        // Validate the User Table ACL.
        internal bool ValidateCRUD(string tableName, CRUD crud)
        {
            try
            {
                // Try to validate the user.
                //AddStatus("Going to Validate CRUD");
                if (tableName.EndsWith("_"))
                {
                    // The Table is a system table.
                    // Users are not allowed to perform CRUD over these table directly.
                    return false;
                }
                else
                {
                    // The Table is a normal table. 
                    bool isValidated = false;
                    // Validate the CRUD.
                    new Select(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                        .FromSystem("tables_acl_")
                        .AddWhere(new Where("table_id", Where.Type.EQUAL, tableName))
                        .AddWhere(new Where("user_id", Where.Type.EQUAL, user.GetUserID()))
                        .SetOnDataResult((List<Row> rows) =>
                        {
                            try
                            {
                                // Lets validate if the user is allowed to perform the required CRUD on this table.
                                if (rows.Count > 0)
                                {
                                    // Lets Get the Validation of the User according to this table.
                                    Row row = rows[0];
                                    if (row.GetBoolean("offline_only"))
                                    {
                                        // This read should be allowed.
                                        isValidated = true;
                                    }
                                    else
                                    {
                                        switch (crud)
                                        {
                                            case CRUD.READ:
                                                if (row.GetSmallInt("read") == 1) { isValidated = true; } else { isValidated = false; }
                                                break;
                                            case CRUD.WRITE:
                                                if (row.GetSmallInt("write") == 1) { isValidated = true; } else { isValidated = false; }
                                                break;
                                            case CRUD.UPDATE:
                                                if (row.GetSmallInt("edit") == 1) { isValidated = true; } else { isValidated = false; }
                                                break;
                                            case CRUD.DELETE:
                                                if (row.GetSmallInt("remove") == 1) { isValidated = true; } else { isValidated = false; }
                                                break;
                                            default:
                                                isValidated = false;
                                                break;
                                        }
                                    }
                                }
                                else
                                {
                                    // The user is not allowed to perform any CRUD operations on this table.
                                    isValidated = false;
                                }
                            }
                            catch (Exception e)
                            {
                                // There was an Error.
                                isValidated = false;
                            }
                        })
                        .Execute();
                    return isValidated;
                }
            }
            catch (Exception e)
            {
                // There was an Errror.
                return false;
            }
        }

        // Validate the User Role RLS.
        internal bool ValidateRoleRLS(long roleID, CRUD crud)
        {
            try
            {
                // Try to validate the user.
                bool isValidated = false;
                // Validate the CRUD.
                new Select(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                    .FromSystem("role_rls_")
                    .AddWhere(new Where("role_id", Where.Type.EQUAL, roleID))
                    .AddWhere(new Where("user_id", Where.Type.EQUAL, user.GetUserID()))
                    .SetOnDataResult((List<Row> rows) =>
                    {
                        try
                        {
                            // Lets validate if the user is allowed to perform the required CRUD on this role.
                            if (rows.Count > 0)
                            {
                                // Lets Get the Validation of the User according to this role.
                                Row row = rows[0];
                                switch (crud)
                                {
                                    case CRUD.READ:
                                        if (row.GetSmallInt("read") == 1) { isValidated = true; } else { isValidated = false; }
                                        break;
                                    case CRUD.WRITE:
                                        if (row.GetSmallInt("write") == 1) { isValidated = true; } else { isValidated = false; }
                                        break;
                                    case CRUD.UPDATE:
                                        if (row.GetSmallInt("edit") == 1) { isValidated = true; } else { isValidated = false; }
                                        break;
                                    case CRUD.DELETE:
                                        if (row.GetSmallInt("remove") == 1) { isValidated = true; } else { isValidated = false; }
                                        break;
                                    default:
                                        isValidated = false;
                                        break;
                                }
                            }
                            else
                            {
                                // The user is not allowed to perform any CRUD operations on this role.
                                isValidated = false;
                            }
                        }
                        catch (Exception e)
                        {
                            // There was an Error.
                            isValidated = false;
                        }
                    })
                    .Execute();
                return isValidated;
            }
            catch (Exception e)
            {
                // There was an Error.
                return false;
            }
        }

        // Validate the User Group RLS.
        internal bool ValidateGroupRLS(long groupID, CRUD crud)
        {
            try
            {
                // Try to validate the user.
                bool isValidated = false;
                // Validate the CRUD.
                new Select(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                    .FromSystem("group_rls_")
                    .AddWhere(new Where("group_id", Where.Type.EQUAL, groupID))
                    .AddWhere(new Where("user_id", Where.Type.EQUAL, user.GetUserID()))
                    .SetOnDataResult((List<Row> rows) =>
                    {
                        try
                        {
                            // Lets validate if the user is allowed to perform the required CRUD on this group.
                            if (rows.Count > 0)
                            {
                                // Lets Get the Validation of the User according to this group.
                                Row row = rows[0];
                                switch (crud)
                                {
                                    case CRUD.READ:
                                        if (row.GetSmallInt("read") == 1) { isValidated = true; } else { isValidated = false; }
                                        break;
                                    case CRUD.WRITE:
                                        if (row.GetSmallInt("write") == 1) { isValidated = true; } else { isValidated = false; }
                                        break;
                                    case CRUD.UPDATE:
                                        if (row.GetSmallInt("edit") == 1) { isValidated = true; } else { isValidated = false; }
                                        break;
                                    case CRUD.DELETE:
                                        if (row.GetSmallInt("remove") == 1) { isValidated = true; } else { isValidated = false; }
                                        break;
                                    default:
                                        isValidated = false;
                                        break;
                                }
                            }
                            else
                            {
                                // The user is not allowed to perform any CRUD operations on this group.
                                isValidated = false;
                            }
                        }
                        catch (Exception e)
                        {
                            // There was an Error.
                            isValidated = false;
                        }
                    })
                    .Execute();
                return isValidated;
            }
            catch (Exception e)
            {
                // There was an Errror.
                return false;
            }
        }

        // Setup the Sync Status Change Listener.
        internal static void Sync(string token, OnSyncStatusChanged onSyncStatusChanged1)
        {
            try
            {
                // Here we will Set the Sync Delegate
                onSyncStatusChanged = onSyncStatusChanged1;
                // Lets call the Syncing Table.
                SyncTables(token);
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // Now we will Sync the Data to the offline system.
        private static void SyncTables(string token)
        {
            try
            {
                // Here we will Sync the offline Tables with the Online ones.
                // Lets sync the system tables.
                int systemTablesSynced = 0;

                // Lets Sync the Roles Table.
                new Waiter()
                    .Url(C.CLOUD_DB_IP_ADDRESS)
                    .Endpoint(C.CLOUD_DB + "/System/Table/Data/Sync")
                    .Method(Waiter.CallMethod.GET)
                    .AddParam(new ParamData("v").Put(0.1))
                    .AddParam(new ParamData("token").Put(token))
                    .AddParam(new ParamData("table_name").Put("roles"))
                    .AddParam(new ParamData("tenancy_type").Put(TableTenancyType.TENANT))
                    .SetOnResponse((bool isSuccessful, string response) =>
                    {
                        try
                        {
                            // Here we will Parse the Data and Insert into the Database.
                            JObject respJObj = JObject.Parse(response);
                            if (respJObj.ContainsKey("success"))
                            {
                                // There are Roles to be inserted.
                                try
                                {
                                    JArray succJArr = (JArray)respJObj.GetValue("success");
                                    for (int i = 0; i < succJArr.Count; i++)
                                    {
                                        // Lets parse the JSON Object.
                                        JObject roleJObj = (JObject)succJArr[i];
                                        // Lets Get the Data of the Role.
                                        // Lets store the Data into the database. 
                                        new Insert(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                            .IntoSystem("roles_")
                                            .PutColumn(new ColumnData("id").Put(roleJObj.GetValue("id")))
                                            .PutColumn(new ColumnData("tenant_id").Put(roleJObj.GetValue("tenant_id")))
                                            .PutColumn(new ColumnData("name").Put(roleJObj.GetValue("name")))
                                            .PutColumn(new ColumnData("parent_id").Put(roleJObj.GetValue("parent_id")))
                                            .PutColumn(new ColumnData("branch").Put(roleJObj.GetValue("branch")))
                                            .PutColumn(new ColumnData("time_stamp").Put(roleJObj.GetValue("time_stamp")))
                                            .SetSyncable(false)
                                            .Execute();
                                    }
                                }
                                finally
                                {
                                    // Once the insert is completed, lets push the information to the User.
                                    systemTablesSynced = systemTablesSynced + 1;
                                    CloudDB.PushSyncStatusChanged(token, SyncStatus.SYSTEM_TABLES, systemTablesSynced);
                                }
                            }
                            else
                            {
                                // There might be no Roles or some Error.
                                if (respJObj.GetValue("error").ToString().Equals("No Data Found"))
                                {
                                    // Once the insert is completed, lets push the information to the User.
                                    systemTablesSynced = systemTablesSynced + 1;
                                    CloudDB.PushSyncStatusChanged(token, SyncStatus.SYSTEM_TABLES, systemTablesSynced);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            // There was an Error.
                        }
                    })
                    .Execute();

                // Lets sync the Group Table.
                new Waiter()
                    .Url(C.CLOUD_DB_IP_ADDRESS)
                    .Endpoint(C.CLOUD_DB + "/System/Table/Data/Sync")
                    .Method(Waiter.CallMethod.GET)
                    .AddParam(new ParamData("v").Put(0.1))
                    .AddParam(new ParamData("token").Put(token))
                    .AddParam(new ParamData("table_name").Put("groups"))
                    .AddParam(new ParamData("tenancy_type").Put(TableTenancyType.TENANT))
                    .SetOnResponse((bool isSuccessful, string response) =>
                    {
                        try
                        {
                            // Here we will Parse the Data and Insert into the Database.
                            JObject respJObj = JObject.Parse(response);
                            if (respJObj.ContainsKey("success"))
                            {
                                // There are Roles to be inserted.
                                try
                                {
                                    JArray succJArr = (JArray)respJObj.GetValue("success");
                                    for (int i = 0; i < succJArr.Count; i++)
                                    {
                                        // Lets parse the JSON Object.
                                        JObject groupJObj = (JObject)succJArr[i];
                                        // Lets Get the Data of the Group.
                                        // Lets store the Data into the database. 
                                        new Insert(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                            .IntoSystem("groups_")
                                            .PutColumn(new ColumnData("id").Put(groupJObj.GetValue("id")))
                                            .PutColumn(new ColumnData("tenant_id").Put(groupJObj.GetValue("tenant_id")))
                                            .PutColumn(new ColumnData("name").Put(groupJObj.GetValue("name")))
                                            .PutColumn(new ColumnData("time_stamp").Put(groupJObj.GetValue("time_stamp")))
                                            .SetSyncable(false)
                                            .Execute();
                                    }
                                }
                                finally
                                {
                                    // Once the insert is completed, lets push the information to the User.
                                    systemTablesSynced = systemTablesSynced + 1;
                                    CloudDB.PushSyncStatusChanged(token, SyncStatus.SYSTEM_TABLES, systemTablesSynced);
                                }
                            }
                            else
                            {
                                // There might be no Groups or some Error.
                                if (respJObj.GetValue("error").ToString().Equals("No Data Found"))
                                {
                                    // Once the insert is completed, lets push the information to the User.
                                    systemTablesSynced = systemTablesSynced + 1;
                                    CloudDB.PushSyncStatusChanged(token, SyncStatus.SYSTEM_TABLES, systemTablesSynced);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            // There was an Error.
                        }
                    })
                    .Execute();

                // Lets sync the Tables ACL Table.
                new Waiter()
                    .Url(C.CLOUD_DB_IP_ADDRESS)
                    .Endpoint(C.CLOUD_DB + "/System/Table/Data/Sync")
                    .Method(Waiter.CallMethod.GET)
                    .AddParam(new ParamData("v").Put(0.1))
                    .AddParam(new ParamData("token").Put(token))
                    .AddParam(new ParamData("table_name").Put("table_user_map"))
                    .AddParam(new ParamData("tenancy_type").Put(TableTenancyType.USER))
                    .SetOnResponse((bool isSuccessful, string response) =>
                    {
                        try
                        {
                            // Here we will Parse the Data and Insert into the Database.
                            JObject respJObj = JObject.Parse(response);
                            if (respJObj.ContainsKey("success"))
                            {
                                // There are Roles to be inserted.
                                try
                                {
                                    JArray succJArr = (JArray)respJObj.GetValue("success");
                                    for (int i = 0; i < succJArr.Count; i++)
                                    {
                                        // Lets parse the JSON Object.
                                        JObject tableUserMapJObj = (JObject)succJArr[i];
                                        // Lets Get the Data of the Group.
                                        // Lets store the Data into the database. 
                                        new Insert(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                            .IntoSystem("tables_acl_")
                                            .PutColumn(new ColumnData("table_id").Put(tableUserMapJObj.GetValue("table_id")))
                                            .PutColumn(new ColumnData("user_id").Put(tableUserMapJObj.GetValue("user_id")))
                                            .PutColumn(new ColumnData("read").Put(short.Parse(tableUserMapJObj.GetValue("read").ToString())))
                                            .PutColumn(new ColumnData("write").Put(short.Parse(tableUserMapJObj.GetValue("write").ToString())))
                                            .PutColumn(new ColumnData("edit").Put(short.Parse(tableUserMapJObj.GetValue("edit").ToString())))
                                            .PutColumn(new ColumnData("remove").Put(short.Parse(tableUserMapJObj.GetValue("remove").ToString())))
                                            .PutColumn(new ColumnData("time_stamp").Put(tableUserMapJObj.GetValue("time_stamp")))
                                            .SetSyncable(false)
                                            .Execute();
                                    }
                                }
                                finally
                                {
                                    // Once the insert is completed, lets push the information to the User.
                                    systemTablesSynced = systemTablesSynced + 1;
                                    CloudDB.PushSyncStatusChanged(token, SyncStatus.SYSTEM_TABLES, systemTablesSynced);
                                }
                            }
                            else
                            {
                                // There might be no Groups or some Error.
                                if (respJObj.GetValue("error").ToString().Equals("No Data Found"))
                                {
                                    // Once the insert is completed, lets push the information to the User.
                                    systemTablesSynced = systemTablesSynced + 1;
                                    CloudDB.PushSyncStatusChanged(token, SyncStatus.SYSTEM_TABLES, systemTablesSynced);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            // There was an Error.
                        }
                    })
                    .Execute();

                // Lets Sync the Roles RLS Table.
                new Waiter()
                    .Url(C.CLOUD_DB_IP_ADDRESS)
                    .Endpoint(C.CLOUD_DB + "/System/Table/Data/Sync")
                    .Method(Waiter.CallMethod.GET)
                    .AddParam(new ParamData("v").Put(0.1))
                    .AddParam(new ParamData("token").Put(token))
                    .AddParam(new ParamData("table_name").Put("user_role_map"))
                    .AddParam(new ParamData("tenancy_type").Put(TableTenancyType.USER))
                    .SetOnResponse((bool isSuccessful, string response) =>
                    {
                        try
                        {
                            // Here we will Parse the Data and Insert into the Database.
                            JObject respJObj = JObject.Parse(response);
                            if (respJObj.ContainsKey("success"))
                            {
                                // There are Roles to be inserted.
                                try
                                {
                                    JArray succJArr = (JArray)respJObj.GetValue("success");
                                    for (int i = 0; i < succJArr.Count; i++)
                                    {
                                        // Lets parse the JSON Object.
                                        JObject roleMapJObj = (JObject)succJArr[i];
                                        // Lets Get the Data of the Group.
                                        // Lets store the Data into the database. 
                                        new Insert(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                            .IntoSystem("role_rls_")
                                            .PutColumn(new ColumnData("role_id").Put(roleMapJObj.GetValue("role_id")))
                                            .PutColumn(new ColumnData("user_id").Put(roleMapJObj.GetValue("user_id")))
                                            .PutColumn(new ColumnData("read").Put(roleMapJObj.GetValue("read")))
                                            .PutColumn(new ColumnData("write").Put(roleMapJObj.GetValue("write")))
                                            .PutColumn(new ColumnData("edit").Put(roleMapJObj.GetValue("edit")))
                                            .PutColumn(new ColumnData("remove").Put(roleMapJObj.GetValue("remove")))
                                            .PutColumn(new ColumnData("time_stamp").Put(roleMapJObj.GetValue("time_stamp")))
                                            .SetSyncable(false)
                                            .Execute();
                                    }
                                }
                                finally
                                {
                                    // Once the insert is completed, lets push the information to the User.
                                    systemTablesSynced = systemTablesSynced + 1;
                                    CloudDB.PushSyncStatusChanged(token, SyncStatus.SYSTEM_TABLES, systemTablesSynced);
                                }
                            }
                            else
                            {
                                // There might be no Groups or some Error.
                                if (respJObj.GetValue("error").ToString().Equals("No Data Found"))
                                {
                                    // Once the insert is completed, lets push the information to the User.
                                    systemTablesSynced = systemTablesSynced + 1;
                                    CloudDB.PushSyncStatusChanged(token, SyncStatus.SYSTEM_TABLES, systemTablesSynced);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            // There was an Error.
                        }
                    })
                    .Execute();

                // Lets sync the Group RLS Table.
                new Waiter()
                    .Url(C.CLOUD_DB_IP_ADDRESS)
                    .Endpoint(C.CLOUD_DB + "/System/Table/Data/Sync")
                    .Method(Waiter.CallMethod.GET)
                    .AddParam(new ParamData("v").Put(0.1))
                    .AddParam(new ParamData("token").Put(token))
                    .AddParam(new ParamData("table_name").Put("user_group_map"))
                    .AddParam(new ParamData("tenancy_type").Put(TableTenancyType.USER))
                    .SetOnResponse((bool isSuccessful, string response) =>
                    {
                        try
                        {
                            // Here we will Parse the Data and Insert into the Database.
                            JObject respJObj = JObject.Parse(response);
                            if (respJObj.ContainsKey("success"))
                            {
                                // There are Roles to be inserted.
                                try
                                {
                                    JArray succJArr = (JArray)respJObj.GetValue("success");
                                    for (int i = 0; i < succJArr.Count; i++)
                                    {
                                        // Lets parse the JSON Object.
                                        JObject groupMapJObj = (JObject)succJArr[i];
                                        // Lets Get the Data of the Group.
                                        // Lets store the Data into the database. 
                                        new Insert(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                            .IntoSystem("group_rls_")
                                            .PutColumn(new ColumnData("group_id").Put(groupMapJObj.GetValue("group_id")))
                                            .PutColumn(new ColumnData("user_id").Put(groupMapJObj.GetValue("user_id")))
                                            .PutColumn(new ColumnData("read").Put(groupMapJObj.GetValue("read")))
                                            .PutColumn(new ColumnData("write").Put(groupMapJObj.GetValue("write")))
                                            .PutColumn(new ColumnData("edit").Put(groupMapJObj.GetValue("edit")))
                                            .PutColumn(new ColumnData("remove").Put(groupMapJObj.GetValue("remove")))
                                            .PutColumn(new ColumnData("time_stamp").Put(groupMapJObj.GetValue("time_stamp")))
                                            .SetSyncable(false)
                                            .Execute();
                                    }
                                }
                                finally
                                {
                                    // Once the insert is completed, lets push the information to the User.
                                    systemTablesSynced = systemTablesSynced + 1;
                                    CloudDB.PushSyncStatusChanged(token, SyncStatus.SYSTEM_TABLES, systemTablesSynced);
                                }
                            }
                            else
                            {
                                // There might be no Groups or some Error.
                                if (respJObj.GetValue("error").ToString().Equals("No Data Found"))
                                {
                                    // Once the insert is completed, lets push the information to the User.
                                    systemTablesSynced = systemTablesSynced + 1;
                                    CloudDB.PushSyncStatusChanged(token, SyncStatus.SYSTEM_TABLES, systemTablesSynced);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            // There was an Error.
                        }
                    })
                    .Execute();

                // Setup the Table Meta.
                // Lets Fill it up.
                SetupTableMeta();

            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        private static int userTablesSynced;
        private static void SyncUserTables(string token)
        {
            try
            {

                // Here we will Write the Sync Code for all the Other Tables.
                // Lets Get all the Tables that have to be synced.
                userTablesSynced = 0;
                new Select(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                    .FromSystem("tables_")
                    .AddWhere(new Where("syncable", Where.Type.EQUAL, true))
                    .SetOnDataResult((List<Row> tableRows) =>
                    {
                        try
                        {
                            // Here we will get the Rows ie. tables to be synced.
                            numberOfUserTablesToBeSynced = tableRows.Count;

                            try
                            {
                                for (int i = 0; i < tableRows.Count; i++)
                                {
                                    // Lets Extract Each table's data.
                                    try
                                    {
                                        // Run the Table Sync in a Group.
                                        Row tableRow = tableRows[i];
                                        SyncUserTableWithDB(tableRow, token);
                                    }
                                    catch (Exception e)
                                    {
                                        // There was an Error.
                                    }
                                }
                            }
                            catch (Exception er)
                            {
                                // There was an Error.
                            }
                        }
                        catch (Exception e)
                        {
                            // There was an Error.;
                        }
                    })
                    .Execute();
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // This is an Extention to the above sync user table method.
        private static void SyncUserTableWithDB(Row tableRow, string token)
        {
            try
            {
                // Here we wiill Sync with the Required Data.
                new Waiter()
                    .Url(C.CLOUD_DB_IP_ADDRESS)
                    .Endpoint(C.CLOUD_DB + "/Table/Data/Sync")
                    .Method(Waiter.CallMethod.GET)
                    .AddParam(new ParamData("v").Put(0.1))
                    .AddParam(new ParamData("token").Put(token))
                    .AddParam(new ParamData("table_name").Put(tableRow.GetString("id")))
                    .SetOnResponse((bool isSuccessful, string response) =>
                    {
                        try
                        {
                            // Here we will Check if the API was hit properly.
                            if (isSuccessful)
                            {
                                // The API was hit properly.
                                // Parse the Response.
                                JObject respJObj = JObject.Parse(response);
                                // Check if the API was successful.
                                if (respJObj.ContainsKey("success"))
                                {
                                    // The Data was successful.
                                    try
                                    {
                                        // Now we will Extract the Data from the JSON and Store into the database.
                                        // Get the JSON Array.
                                        JArray tableDataJArr = (JArray)respJObj.GetValue("success");
                                        for (int rowsInd = 0; rowsInd < tableDataJArr.Count; rowsInd++)
                                        {
                                            try
                                            {
                                                // Here we will Get the JSON Object and Store the Data.
                                                JObject rowDataJObj = (JObject)tableDataJArr[rowsInd];
                                                // Get the Data and Store in the database
                                                // First we will write in the local table supposed to be written in.
                                                Insert rowInsert = new Insert(C.SYSTEM_USER_DB_PASSCODE)
                                                    .IntoSystemInternal(tableRow.GetString("id"));
                                                // Here we will add the required columns of data.
                                                IList<string> colKeys = rowDataJObj.Properties().Select(p => p.Name).ToList();
                                                for (int i = 0; i < colKeys.Count; i++)
                                                {
                                                    // Add the Column data into the insert.
                                                    if (colKeys[i].EndsWith("_"))
                                                    {
                                                        // This Column is not to be added.
                                                    }
                                                    else
                                                    {
                                                        // This Column is to be added.
                                                        rowInsert.PutColumn(new ColumnData(colKeys[i], rowDataJObj[colKeys[i]]));

                                                    }
                                                }
                                                rowInsert.SetSyncable(false);   // This insert is not syncable.
                                                try
                                                {
                                                    // Set the other details of the insert.
                                                    rowInsert.SetSyncID(rowDataJObj["sync_id_"].ToString());
                                                }
                                                catch (Exception e)
                                                {
                                                    // There was an Error.
                                                }
                                                try
                                                {
                                                    // Set the Tenant data if it is there.
                                                    if (rowDataJObj.ContainsKey("tenant_id_"))
                                                    {
                                                        // There is a Tenant ID.
                                                        rowInsert.SetTenantID(long.Parse(rowDataJObj["tenant_id_"].ToString()));
                                                    }
                                                }
                                                catch (Exception er)
                                                {
                                                    // There was an Error.
                                                }

                                                // Now we will add the RLS details.
                                                try
                                                {
                                                    // Set the RLS Data.
                                                    if (int.Parse(rowDataJObj["rls_type_"].ToString()) == 1)
                                                    {
                                                        // This is a role.
                                                        rowInsert.SetRoleID(long.Parse(rowDataJObj["rls_id_"].ToString()));
                                                        //AddStaticStatus(" Role : " + rowDataJObj["rls_id_"].ToString());
                                                    }
                                                    else if (int.Parse(rowDataJObj["rls_type_"].ToString()) == 2)
                                                    {
                                                        // This is a role.
                                                        rowInsert.SetGroupID(long.Parse(rowDataJObj["rls_id_"].ToString()));
                                                        //AddStaticStatus(" Group : " + rowDataJObj["rls_id_"].ToString());
                                                    }
                                                }
                                                catch (Exception e)
                                                {
                                                    // There was an Error.
                                                }
                                                // Set the Update time of the row.
                                                try
                                                {
                                                    // Lets set the update time of the row.
                                                    if (rowDataJObj.ContainsKey("update_time_"))
                                                    {
                                                        // There is the update time column.
                                                        rowInsert.SetUpdateTime(long.Parse(rowDataJObj["update_time_"].ToString()));
                                                    }
                                                }
                                                catch (Exception e)
                                                {
                                                    // There was an Error.
                                                }
                                                // Lets Execute the insert process.
                                                int rowsInserted = rowInsert.Execute();

                                                // Now we will insert into the local sync table.
                                                try
                                                {
                                                    // Now we will store the data that has to be synced into the system tables to maintain offline syncing.
                                                    // Store into the Sync Offline Table.
                                                    Insert offlineSync = new Insert(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                                                        .IntoSystem("synced_check_")
                                                    // Now lets sync into the Sync Check Table.
                                                        .PutColumn(new ColumnData("id").Put(rowDataJObj["sync_id_"]))
                                                        .PutColumn(new ColumnData("crud_type").Put("INSERT"))
                                                        .PutColumn(new ColumnData("table_name").Put(rowDataJObj["table_name"]));
                                                    // Insert the Data in the insert.
                                                    offlineSync.PutColumn(new ColumnData("data").Put(rowDataJObj.ToString()))
                                                        .PutColumn(new ColumnData("is_self_written").Put(false))
                                                        .SetSyncable(false)
                                                        .Execute();
                                                }
                                                catch (Exception e)
                                                {
                                                    // There was an Error in the Syncing process.
                                                }
                                            }
                                            catch (Exception e)
                                            {
                                                // There was a Error.
                                            }
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        // There was an Error.
                                        // Maybe the UI was closed.
                                    }
                                    finally
                                    {
                                        // Once the insert is completed, lets push the information to the User.
                                        userTablesSynced = userTablesSynced + 1;
                                        CloudDB.PushSyncStatusChanged(token, SyncStatus.USER_TABLES, userTablesSynced);
                                    }
                                }
                                else
                                {
                                    // There probably are no rows in the table.
                                    // Once the insert is completed, lets push the information to the User.
                                    userTablesSynced = userTablesSynced + 1;
                                    CloudDB.PushSyncStatusChanged(token, SyncStatus.USER_TABLES, userTablesSynced);
                                }
                            }
                            else
                            {
                                // The API was not hit properly.
                            }
                        }
                        catch (Exception e)
                        {
                            // There was an Error.
                        }
                    })
                    .Execute();
            }
            catch (Exception er)
            {
                // There was an Error.
            }
        }

        // We will use this Method to push the Changes of the Status to the User.
        private static void PushSyncStatusChanged(string token, SyncStatus syncStatus, int syncedNum)
        {
            try
            {
                // Here we will Check the status and send to the user if required.
                if (syncStatus == SyncStatus.SYSTEM_TABLES)
                {
                    // It is the System Tables that are getting sycned.
                    if (syncedNum == 5)  // Number of System Tables for the Offline Working.
                    {
                        // All the System Tables have been sycned.
                        onSyncStatusChanged(syncStatus, true);
                        // Lets start the syncing of user tables.
                        SyncUserTables(token);
                    }
                }
                else// if (syncStatus == SyncStatus.USER_TABLES)
                {
                    // It is the User Tables that are getting synced.
                    if (syncedNum == numberOfUserTablesToBeSynced) // Number of Rows synced.
                    {
                        // All the user tables that were required to be synced are synced.
                        onSyncStatusChanged(syncStatus, true);
                    }
                }
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // Check if the Table is to be Syncable.
        public static bool IsSyncable(string tableID)
        {
            try
            {
                // Check if the Table is Multitenant.
                return tableMeta[tableID][0] == 1;
            }
            catch (Exception e)
            {
                // There was an Error.
                return false;
            }
        }

        // Check if the Table is an Offline Only Table.
        public static bool IsOfflineOnly(string tableID)
        {
            try
            {
                // Check if the Table is Multitenant.
                return tableMeta[tableID][1] == 1;
            }
            catch (Exception e)
            {
                // There was an Error.
                return false;
            }
        }

        // Check if the Table is a Multi-tenant table.
        public static bool IsMultiTenant(string tableID)
        {
            try
            {
                // Check if the Table is Multitenant.
                return tableMeta[tableID][2] == 1;
            }
            catch (Exception e)
            {
                // There was an Error.
                return false;
            }
        }

        // Return the Session to the Parent Object.
        internal SQLiteCommand GetSession()
        {
            return dbConn.GetCmd();
        }

        // Here we set the status and the status trace.
        private void AddStatus(string status)
        {
            try
            {
                // Here we append the status to the status trace and status.
                if (StatusTrace == null)
                {
                    StatusTrace = new StringBuilder();
                }
                Status = status;
                StatusTrace.Append(status)
                    .Append('\n');
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        private static void AddStaticStatus(string status)
        {
            try
            {
                // Here we append the status to the status trace and status.
                if (StaticStatusTrace == null)
                {
                    StaticStatusTrace = new StringBuilder();
                }
                StaticStatus = status;
                StaticStatusTrace.Append(status)
                    .Append('\n');
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        public static string GetStaticStatusTrace()
        {
            return StaticStatusTrace.ToString();
        }

        // This will retrieve the whole status trace.
        public string GetStatusTrace()
        {
            return StatusTrace.ToString();
        }

        // This will retrieve just the status of the cloud object.
        public string GetStatus()
        {
            return Status;
        }

        // Convert all the Rows from SQL Reader Object to the Rows Object.
        internal static List<Row> SQLiteReaderToRows(SQLiteDataReader sdr)
        {
            try
            {
                // Here we will convert the sqlite data reader.
                // Lets extract the data from the reader.
                if (sdr.HasRows)
                {
                    // It has rows to extract.
                    List<Row> rows = new List<Row>();
                    while (sdr.Read())
                    {
                        // Lets extract the row data.
                        Row row = new Row();
                        for (int i = 0; i < sdr.FieldCount; i++)
                        {
                            // Here we will Extract the Column Name.
                            row.AddColumn(sdr.GetName(i), sdr.GetValue(i));
                        }
                        // Now we will add it to the Row List.
                        rows.Add(row);
                    }
                    return rows;
                }
                else
                {
                    // Now Rows to Extract.
                    return new List<Row>();
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                return new List<Row>();
            }
        }

        // The Where IN Clause Generator.
        internal string RLSWhere(QueryType queryType, string tableName = "")
        {
            try
            {
                // Here we will create the where in clause.
                StringBuilder whereBuild = new StringBuilder();
                whereBuild.Append(" ( ");

                // Lets add the RLS for the Role RLS.
                StringBuilder roleBuild = new StringBuilder();
                try
                {
                    // Lets Add the Role RLS.
                    // Get the Roles of the User.
                    SQLiteCommand dbSess = new DBConnector().GetCmd();
                    dbSess.Reset();
                    dbSess.CommandText = "SELECT * FROM role_rls_ WHERE user_id = @user_id";
                    SQLiteParameter sqlParam = new SQLiteParameter("user_id", user.GetUserID());
                    dbSess.Parameters.Add(sqlParam);
                    dbSess.Prepare();
                    List<Row> rows = SQLiteReaderToRows(dbSess.ExecuteReader());
                    dbSess.Dispose();
                    // Now lets add the RLS.
                    if (tableName == "")
                    {
                        // There is No Extra Table Name Given.
                        roleBuild.Append(" (rls_id_ IN (");
                    }
                    else
                    {
                        // An Extra Table Name is Given, Lets Respect that.
                        roleBuild.Append(" (" +
                            tableName +
                            ".rls_id_ IN (");
                    }

                    // Lets now add the role rls.
                    string rlsQuery = "";
                    switch (queryType)
                    {
                        case QueryType.SELECT:
                            rlsQuery = "read";
                            break;
                        case QueryType.INSERT:
                            rlsQuery = "write";
                            break;
                        case QueryType.UPDATE:
                            rlsQuery = "edit";
                            break;
                        case QueryType.DELETE:
                            rlsQuery = "remove";
                            break;
                    }
                    StringBuilder tempRoleBuild = new StringBuilder();
                    for (int i = 0; i < rows.Count; i++)
                    {
                        // Now we will add the rows.
                        Row row = rows[i];
                        if (row.GetInt(rlsQuery) == 1)
                        {
                            // The user is allowed to perform the CRUD over this Role.
                            if (tempRoleBuild.Length > 0)
                            {
                                tempRoleBuild.Append(", ");
                            }
                            tempRoleBuild.Append(row.GetString("role_id"));
                        }
                    }
                    roleBuild.Append(tempRoleBuild);
                    if (rows.Count == 0)
                    {
                        roleBuild.Append(" -1 ");
                    }

                    if (tableName == "")
                    {
                        // There is No Extra Table Name Given.
                        roleBuild.Append(" ) AND rls_type_ = 1) ");
                    }
                    else
                    {
                        // An Extra Table Name is Given, Lets Respect that.
                        roleBuild.Append(" ) AND " +
                            tableName +
                            ".rls_type_ = 1) ");
                    }
                }
                catch (Exception e)
                {
                    // There was an Error.
                }

                // Lets add the RLS for the Group RLS.
                StringBuilder groupBuild = new StringBuilder();
                try
                {
                    // Lets Add the Group RLS.
                    // Get the Groups of the User.
                    SQLiteCommand dbSess = new DBConnector().GetCmd();
                    dbSess.Reset();
                    dbSess.CommandText = "SELECT * FROM group_rls_ WHERE user_id = @user_id";
                    SQLiteParameter sqlParam = new SQLiteParameter("user_id", user.GetUserID());
                    dbSess.Parameters.Add(sqlParam);
                    dbSess.Prepare();
                    List<Row> rows = SQLiteReaderToRows(dbSess.ExecuteReader());
                    dbSess.Dispose();
                    // Now lets add the RLS.
                    if (tableName == "")
                    {
                        // There is No Extra Table Name Given.
                        groupBuild.Append(" (rls_id_ IN (");
                    }
                    else
                    {
                        // An Extra Table Name is Given, Lets Respect that.
                        groupBuild.Append(" (" +
                            tableName +
                            ".rls_id_ IN (");
                    }
                    // Lets now add the group rls.
                    string rlsQuery = "";
                    switch (queryType)
                    {
                        case QueryType.SELECT:
                            rlsQuery = "read";
                            break;
                        case QueryType.INSERT:
                            rlsQuery = "write";
                            break;
                        case QueryType.UPDATE:
                            rlsQuery = "edit";
                            break;
                        case QueryType.DELETE:
                            rlsQuery = "remove";
                            break;
                    }
                    StringBuilder tempGroupBuild = new StringBuilder();
                    for (int i = 0; i < rows.Count; i++)
                    {
                        // Now we will add the rows.
                        Row row = rows[i];
                        if (row.GetInt(rlsQuery) == 1)
                        {
                            // The user is allowed to perform the CRUD over this Role.
                            if (tempGroupBuild.Length > 0)
                            {
                                tempGroupBuild.Append(", ");
                            }
                            tempGroupBuild.Append(row.GetString("group_id"));
                        }
                    }
                    groupBuild.Append(tempGroupBuild);
                    if (rows.Count == 0)
                    {
                        groupBuild.Append(" -1 ");
                    }

                    if (tableName == "")
                    {
                        // There is No Extra Table Name Given.
                        groupBuild.Append(" ) AND rls_type_ = 2) ");
                    }
                    else
                    {
                        // An Extra Table Name is Given, Lets Respect that.
                        groupBuild.Append(" ) AND " +
                            tableName +
                            ".rls_type_ = 2) ");
                    }
                }
                catch (Exception e)
                {
                    // There was an Error.
                }

                // Add the Role & Group Build to the Where Build.
                // Here we will Validate if the RLS has worked or not.
                /*if (roleBuild.Length == 0 && groupBuild.Length == 0)
                {
                    // The RLS has not worked.
                    whereBuild = new StringBuilder();
                }
                else if (roleBuild.Length == 0)
                {
                    // Only the Group RLS has worked
                    whereBuild.Append(roleBuild);
                    whereBuild.Append(" ) ");
                }
                else if (groupBuild.Length == 0)
                {
                    // Only the Role RLS has worked.
                    whereBuild.Append(groupBuild);
                    whereBuild.Append(" ) ");
                }
                else
                {
                    // All the RLS has worked.
                    whereBuild.Append(roleBuild)
                        .Append(" OR ")
                        .Append(groupBuild);

                    whereBuild.Append(" ) ");
                }*/
                whereBuild.Append(roleBuild)
                    .Append(" OR ")
                    .Append(groupBuild);

                whereBuild.Append(" ) ");
                return whereBuild.ToString();
            }
            catch (Exception e)
            {
                // There was an Error.
                return "";
            }
        }

        // Lets delete the user's data accordingly.
        internal bool DeleteUser()
        {
            try
            {
                // Here we will Delete the User.
                // Check if other users exist.
                SQLiteCommand dbSess = new DBConnector().GetCmd();
                dbSess.Reset(); // Clean the Session.
                dbSess.CommandText = "DELETE FROM users_ WHERE token = @token";
                SQLiteParameter sqlParam = new SQLiteParameter("token", user.GetToken());
                dbSess.Parameters.Add(sqlParam);
                dbSess.Prepare();
                bool deleted = dbSess.ExecuteNonQuery() > 0 ? true : false;
                dbSess.Dispose();
                // Lets Unregister the user token with the server.
                if (deleted)
                {
                    // Lets now unregister the user with this device.

                }
                return deleted;


                /*// Create the DB Connection.
                SQLiteCommand dbSess = new DBConnector().GetCmd();
                dbSess.Reset(); // Clean the Session.
                // Lets now search for the users.
                dbSess.CommandText = "SELECT * FROM users_";
                // Lets prepare the statement.
                dbSess.Prepare();
                // Lets Execute the Query.
                SQLiteDataReader sdr = dbSess.ExecuteReader();
                List<Row> users_ = SQLiteReaderToRows(sdr);
                // User's Data Begin.
                long tenantId = 0;
                string userID = "";

                // User's Data End
                if (users_.Count > 1)
                {
                    // There are other users.
                    // Lets only delete the User details here.
                    Dictionary<long, int> tenantIDs = new Dictionary<long, int>();
                    for (int i = 0; i < users_.Count; i++)
                    {
                        // Now lets compare with the list.
                        Row userRow = users_[i];
                        if (tenantIDs.ContainsKey(userRow.GetBigInt("tenant_id")))
                        {
                            // There is a tenant id in the list.
                            int tenantCount = tenantIDs[userRow.GetBigInt("tenant_id")];
                            tenantIDs[userRow.GetBigInt("tenant_id")] = tenantCount + 1;
                        }
                        else
                        {
                            // There is no tenant id, lets add it.
                            tenantIDs[userRow.GetBigInt("tenant_id")] = 1;
                        }
                        // Check the token of the user, and get the tenant of the user.
                        if (userRow.GetString("token").Equals(token))
                        {
                            // This is the user's row.
                            // Get the Required user data.
                            tenantId = userRow.GetBigInt("tenant_id");
                            userID = userRow.GetString("user_id");
                        }
                    }
                    // Lets check how many tenants are loaded up with the data.
                    bool deleteTenantData = false;
                    if (tenantIDs.Count > 1)
                    {
                        // There are more than 1 tenant.
                        if (tenantIDs[tenantId] > 1)
                        {
                            // There is more than one user of the same tenant.
                            deleteTenantData = false;
                        }
                        else
                        {
                            // There is only one user of this tenant.
                            // We can delete the tenant data.
                            deleteTenantData = true;
                        }
                    }
                    else
                    {
                        // There is only 1 tenant.
                        deleteTenantData = false;
                    }

                    // Delete the Tenant's Data or only the user's data.
                    if (deleteTenantData)
                    {
                        // Lets delete the tenant data.
                        // Lets Delete all the Data.
                        new Select(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                            .FromSystem("tables_")
                            .SetOnDataResult((List<Row> tableRows) =>
                            {
                                try
                                {
                                    // Lets now delete from all the tables, according to the list of tables.
                                    for (int i = 0; i < tableRows.Count; i++)
                                    {
                                        try
                                        {
                                            // Here we will delete from the table.
                                            Row tableRow = tableRows[i];
                                            dbSess.Reset();
                                            dbSess.CommandText = "DELETE FROM " + tableRow.GetString("id")
                                                    + " WHERE tenant_id_ = @tenant_id";
                                            SQLiteParameter sqlParam = new SQLiteParameter("tenant_id", tenantId);
                                            dbSess.Parameters.Add(sqlParam);
                                            dbSess.Prepare();
                                            dbSess.ExecuteNonQuery();
                                        }
                                        catch (Exception e)
                                        {
                                            // There was an Error.
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    // There was an Error.
                                }
                            })
                            .Execute();
                        // Lets now delete the system tables data.
                        new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                            .FromSystem("roles_")
                            .AddWhere(new Where("tenant_id", Where.Type.EQUAL, tenantId))
                            .Execute();
                        new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                            .FromSystem("groups_")
                            .AddWhere(new Where("tenant_id", Where.Type.EQUAL, tenantId))
                            .Execute();
                        new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                            .FromSystem("tables_acl_")
                            .AddWhere(new Where("user_id", Where.Type.EQUAL, userID))
                            .Execute();
                        new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                            .FromSystem("users_")
                            .AddWhere(new Where("user_id", Where.Type.EQUAL, userID))
                            .Execute();
                        new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                            .FromSystem("role_rls_")
                            .AddWhere(new Where("user_id", Where.Type.EQUAL, userID))
                            .Execute();
                        new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                            .FromSystem("group_rls_")
                            .AddWhere(new Where("user_id", Where.Type.EQUAL, userID))
                            .Execute();
                        new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                            .FromSystem("sync_offline_")
                            .AddWhere(new Where("token", Where.Type.EQUAL, token))
                            .Execute();
                        new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                            .FromSystem("synced_check_")
                            .Execute();
                    }
                    else
                    {
                        // Lets delete only the user's data.
                        new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                            .FromSystem("users_")
                            .AddWhere(new Where("token", Where.Type.EQUAL, token))
                            .Execute();
                    }
                }
                else
                {
                    // There are no other users.
                    // Lets Delete all the Data.
                    new Select(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                        .FromSystem("tables_")
                        .SetOnDataResult((List<Row> tableRows) =>
                        {
                            try
                            {
                                // Lets now delete from all the tables, according to the list of tables.
                                for (int i = 0; i < tableRows.Count; i++)
                                {
                                    try
                                    {
                                        // Here we will delete from the table.
                                        Row tableRow = tableRows[i];
                                        dbSess.Reset();
                                        dbSess.CommandText = "DELETE FROM " + tableRow.GetString("id");
                                        dbSess.Prepare();
                                        int deleted = dbSess.ExecuteNonQuery();
                                        AddStaticStatus("Deleted : " + tableRow.GetString("id") + " : " + deleted);
                                    }
                                    catch (Exception e)
                                    {
                                        // There was an Error.
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                // There was an Error.
                            }
                        })
                        .Execute();
                    AddStaticStatus("Deleted All User Tables");
                    // Lets now delete the system tables data.
                    Delete del = new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                        .FromSystem("roles_");
                    del.Execute();
                    AddStaticStatus("Deleted roles_ : " + del.GetStatusTrace());
                    del = new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                        .FromSystem("groups_");
                    del.Execute();
                    AddStaticStatus("Deleted groups_ : " + del.GetStatusTrace());
                    del = new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                        .FromSystem("tables_acl_");
                    del.Execute();
                    AddStaticStatus("Deleted tables_acl_ : " + del.GetStatusTrace());
                    del = new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                        .FromSystem("users_");
                    del.Execute();
                    AddStaticStatus("Deleted users_ : " + del.GetStatusTrace());
                    del = new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                        .FromSystem("role_rls_");
                    del.Execute();
                    AddStaticStatus("Deleted role_rls_ : " + del.GetStatusTrace());
                    del = new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                        .FromSystem("group_rls_");
                    del.Execute();
                    AddStaticStatus("Deleted group_rls_ : " + del.GetStatusTrace());
                    del = new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                        .FromSystem("sync_offline_");
                    del.Execute();
                    AddStaticStatus("Deleted sync_offline_ : " + del.GetStatusTrace());
                    del = new Delete(new CloudDB(C.SYSTEM_USER_DB_PASSCODE))
                        .FromSystem("synced_check_");
                    del.Execute();
                    AddStaticStatus("Deleted synced_check_ : " + del.GetStatusTrace());
                }
                return true;*/
            }
            catch (Exception e)
            {
                // There was an Error.
                return false;
            }
        }

        // This will be used to delete / drop all the tables in the database.
        // Mostly to be used in only development environments.
        internal bool Destroy(List<Row> rows)
        {
            try
            {
                // Here we will clean the database and delete all the tables.
                return dbConn.DestroyTBs(rows);
            }
            catch (Exception e)
            {
                // There was an Error
                return false;
            }
        }

        // This will be used to Clean / Truncate all the tables in the database.
        internal bool Clean(List<Row> rows)
        {
            try
            {
                // Here we will clean the database and delete all the tables.
                return dbConn.CleanTBs(rows);
            }
            catch (Exception e)
            {
                // There was an Error
                return false;
            }
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

            // Here we will Delete all the data on the offline database.
            internal bool DestroyTBs(List<Row> rows)
            {
                try
                {
                    // Here we will Remove all the database data.
                    SQLiteCommand dbSess = new SQLiteCommand(null, conn);
                    dbSess.CommandText = "PRAGMA foreign_keys = OFF";
                    foreignKeyEnabled = dbSess.ExecuteNonQuery() > 0;

                    foreach (Row row in rows)
                    {
                        try
                        {
                            dbSess.CommandText = "DROP TABLE " + row.GetString("name");
                            dbSess.ExecuteNonQuery();
                        }
                        catch (Exception e)
                        {
                            // There was an Error.
                        }
                    }
                    dbSess.CommandText = "PRAGMA foreign_keys = ON";
                    foreignKeyEnabled = dbSess.ExecuteNonQuery() > 0;
                    return true;
                }
                catch (Exception e)
                {
                    // There was an Error.
                    AddStaticStatus("Err : " + e.Message);
                    return false;
                }
            }

            // Here we will Clean all the Tables to be Cleaned.
            internal bool CleanTBs(List<Row> rows)
            {
                try
                {
                    // Here we will Remove all the database data.
                    SQLiteCommand dbSess = new SQLiteCommand(null, conn);
                    dbSess.CommandText = "PRAGMA foreign_keys = OFF";
                    foreignKeyEnabled = dbSess.ExecuteNonQuery() > 0;

                    foreach (Row row in rows)
                    {
                        try
                        {
                            dbSess.CommandText = "TRUNCATE TABLE " + row.GetString("name");
                            dbSess.ExecuteNonQuery();
                        }
                        catch (Exception e)
                        {
                            // There was an Error.
                        }
                    }
                    dbSess.CommandText = "PRAGMA foreign_keys = ON";
                    foreignKeyEnabled = dbSess.ExecuteNonQuery() > 0;
                    return true;
                }
                catch (Exception e)
                {
                    // There was an Error.
                    return false;
                }
            }

        }

        // The Row Level Security Data Holder Class.
        public class RowLevelSecurity
        {
            // Getters & Setters.
            public long ID { get; set; }
            public short Type { get; set; }
        }
    }
}
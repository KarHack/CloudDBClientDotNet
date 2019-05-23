using _36E_Business___ERP.helper;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace _36E_Business___ERP.cloudDB
{
    class Update
    {

        // Variables.
        private CloudDB cdb;
        private string status;
        private StringBuilder statusTrace;
        private string tableName;
        private StringBuilder columnNameBuild;
        private StringBuilder whereClauseBuilder;
        private Dictionary<string, object> colBindObjs;
        private Dictionary<string, object> whereBindObjs;
        private bool isError = false;
        private long tenantID = 0;
        private short rlsType;
        private string syncID;
        private long rlsID = 0;
        private bool syncable = true;   // This will tell the system if this update is to be synced with the cloud.
        private JObject columnsJObj;
        private JObject wheresJObj;
        private long updateTimeI = 0;

        // Callbacks.
        // This will be used to send the insert number to the user if the listener is set.
        public delegate void OnDataUpdated(List<Row> updatedRows);
        private OnDataUpdated onDataUpdated = null;


        // Static Constant Values.
        // The Type of Operation Update Column.
        public enum O
        {
            ADD, SUBSCRACT, MULTIPLY, DIVIDE, MODULAR
        }

        // The Type of Update Column
        public static class T
        {
            public const string VALUE = "VALUE";
            public const string COLUMN = "COLUMN";
            public const string CONCAT = "CONCAT";
            public const string OPERATION = "OPERATION";
        }

        // Constructor.
        // The usable constructor.
        // The Cloud DB Constructor.
        public Update(CloudDB cdb)
        {
            try
            {
                // Lets authenticate the user.
                // Initialize the Cloud DB Service.
                if (cdb.IsSystemUser || cdb.user.IsActive)
                {
                    // The user is authenticated.
                    this.cdb = cdb;     // Set the Cloud DB Service.
                    AddStatus("User is Authenticated");
                    columnNameBuild = new StringBuilder();
                    whereClauseBuilder = new StringBuilder();
                    colBindObjs = new Dictionary<string, object>();
                    whereBindObjs = new Dictionary<string, object>();
                    columnsJObj = new JObject();
                    wheresJObj = new JObject();
                }
                else
                {
                    // The user is not authenticated.
                    isError = true;
                    AddStatus("User is NOT Authenticated");
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                isError = true;
                AddStatus("Error : " + e.Message);
            }
        }

        // The User Token will be directly provided.
        public Update(string token)
        {
            try
            {
                // Lets authenticate the user.
                // Initialize the Cloud DB Service.
                CloudDB cdb = new CloudDB(token);
                if (cdb.IsSystemUser || cdb.user.IsActive)
                {
                    // The user is authenticated.
                    this.cdb = cdb;     // Set the Cloud DB Service.
                    AddStatus("User is Authenticated");
                    columnNameBuild = new StringBuilder();
                    whereClauseBuilder = new StringBuilder();
                    colBindObjs = new Dictionary<string, object>();
                    whereBindObjs = new Dictionary<string, object>();
                    columnsJObj = new JObject();
                    wheresJObj = new JObject();
                }
                else
                {
                    // The user is not authenticated.
                    isError = true;
                    AddStatus("User is NOT Authenticated");
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                isError = true;
                AddStatus("Error : " + e.Message);
            }
        }

        // This will be used to add the table name to be updated.
        public Update Into(string tableName)
        {
            try
            {
                // Here we Add the table name to be updated.
                if (this.tableName == null)
                {
                    //  This the First time the table name is being set.
                    if (cdb.ValidateCRUD(tableName, CloudDB.CRUD.UPDATE) && cdb.user.IsActive)
                    {
                        // The user is allowed to update into this table.
                        this.tableName = Regex.Replace(tableName, " ", "");
                        // Initiate the Required Variables.
                        AddStatus("User is Authorized to Update this Table");
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
                        // The user is not allowed to update into this table.
                        AddStatus("User is NOT Authorized to Update this Table");
                        isError = true;
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
                AddStatus("Table Error : " + e.Message);
                isError = true;
                return this;
            }
        }

        public Update IntoSystem(string tableName)
        {
            try
            {
                // Here we Add the table name to be updated.
                if (this.tableName == null && Helper.DBValidation.IsSystemTB(tableName) && cdb.IsSystemUser)
                {
                    // The user is allowed to update into this table.
                    syncable = false;
                    this.tableName = Regex.Replace(tableName, " ", "");
                    // Initiate the Required Variables.
                    return this;
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
                AddStatus("Table Error : " + e.Message);
                isError = true;
                return this;
            }
        }


        /* Different Types of Updates available. */
        // Set the Columns needed to the user.
        public Update AddColumn(string columnName, object columnValue)
        {
            try
            {
                // Here we get the Column Name and the Column Value to be added by the User.
                // Check if this is the Second Column and Add the comma.
                // Only let to proceed if the Column does'nt have the special rows.
                if (columnName.ElementAt(columnName.Length - 1) == '_')
                {
                    // This column is not a valid column and must be left only for the system to edit.
                    AddStatus("User cannot edit " + columnName + " System Column");
                }
                else
                {
                    // This Columns is valid.
                    if (columnNameBuild.Length > 0)
                    {
                        columnNameBuild.Append(", ");
                    }
                    // Add the Column to the Column Name Query string.
                    columnName = Regex.Replace(columnName, " ", "");
                    columnNameBuild.Append(columnName)
                        .Append(" = ")
                        .Append("@C")
                        .Append(columnName)
                        .Append(' ');
                    colBindObjs.Add("C" + columnName, columnValue);

                    // If the Table is a syncable table then we will Add this Column to the Column JSON.
                    try
                    {
                        // Here we will Add the Column to the JSON.
                        if (syncable)
                        {
                            // Create the Json Object.
                            JObject colJObj = new JObject();
                            colJObj.Add("value", columnValue.ToString());
                            colJObj.Add("type", "VALUE");
                            // Store the Column JSON to the JSON Columns Object.
                            columnsJObj.Add(columnName, colJObj);
                        }
                    }
                    catch (Exception er)
                    {
                        // There was an Error.
                    }
                }
                return this;

            }
            catch (Exception e)
            {
                // There was an Error.
                isError = true;
                AddStatus("Add Column Error : " + e.Message);
                return this;
            }
        }

        // Set the Columns needed to the user to make string operations with another column.
        public Update AddColumn(string columnName, string otherColumnName, bool isRefereningColumn)
        {
            try
            {
                // Here we get the Column Name and the Column Value to be added by the User.
                // Check if this is the Second Column and Add the comma.
                // Only let to proceed if the Column does'nt have the special rows.
                if (isRefereningColumn)
                {
                    if (columnName.ElementAt(columnName.Length - 1) == '_')
                    {
                        // This column is not a valid column and must be left only for the system to edit.
                        AddStatus("User cannot edit " + columnName + " System Column");
                    }
                    else
                    {
                        // This Columns is valid.
                        if (columnNameBuild.Length > 0)
                        {
                            columnNameBuild.Append(", ");
                        }
                        // Add the Column to the Column Name Query string.
                        columnNameBuild.Append(Regex.Replace(columnName, " ", ""))
                            .Append(" = ")
                            .Append(Regex.Replace(otherColumnName, " ", ""));

                        // If the Table is a syncable table then we will Add this Column to the Column JSON.
                        try
                        {
                            // Here we will Add the Column to the JSON.
                            if (syncable)
                            {
                                // Create the Json Object.
                                JObject colJObj = new JObject();
                                colJObj.Add("column", otherColumnName);
                                colJObj.Add("type", "COLUMN");
                                // Store the Column JSON to the JSON Columns Object.
                                columnsJObj.Add(columnName, colJObj);
                            }
                        }
                        catch (Exception er)
                        {
                            // There was an Error.
                        }
                    }
                }
                else
                {
                    if (columnName.ElementAt(columnName.Length - 1) == '_')
                    {
                        // This column is not a valid column and must be left only for the system to edit.
                        AddStatus("User cannot edit " + columnName + " System Column");
                    }
                    else
                    {
                        // This Columns is valid.
                        if (columnNameBuild.Length > 0)
                        {
                            columnNameBuild.Append(", ");
                        }
                        // Add the Column to the Column Name Query string.
                        columnName = Regex.Replace(columnName, " ", "");
                        columnNameBuild.Append(columnName)
                            .Append(" = ")
                            .Append("@C")
                            .Append(columnName)
                            .Append(' ');
                        colBindObjs.Add("C" + columnName, otherColumnName);

                        // If the Table is a syncable table then we will Add this Column to the Column JSON.
                        try
                        {
                            // Here we will Add the Column to the JSON.
                            if (syncable)
                            {
                                // Create the Json Object.
                                JObject colJObj = new JObject();
                                colJObj.Add("value", otherColumnName);
                                colJObj.Add("type", "VALUE");
                                // Store the Column JSON to the JSON Columns Object.
                                columnsJObj.Add(columnName, colJObj);
                            }
                        }
                        catch (Exception er)
                        {
                            // There was an Error.
                        }
                    }
                }
                return this;
            }
            catch (Exception e)
            {
                // There was an Error.
                isError = true;
                AddStatus("Add Column Error : " + e.Message);
                return this;
            }
        }

        // Set the Column to allow to update the string with a concatinate of a column data and another string.
        public Update AddColumn(string columnName, string concatColumnName, string concatString, bool isAfterColumn)
        {
            try
            {
                // Here we get the Column Name and the Column Value to be added by the User.
                // Check if this is the Second Column and Add the comma.
                // Only let to proceed if the Column does'nt have the special rows.
                if (columnName.ElementAt(columnName.Length - 1) == '_')
                {
                    // This column is not a valid column and must be left only for the system to edit.
                    AddStatus("User cannot edit " + columnName + " System Column");
                }
                else
                {
                    // This Columns is valid.
                    if (columnNameBuild.Length > 0)
                    {
                        columnNameBuild.Append(", ");
                    }
                    // Add the Column to the Column Name Query string.
                    columnName = Regex.Replace(columnName, " ", "");
                    columnNameBuild.Append(columnName)
                        .Append(" = ");

                    if (isAfterColumn)
                    {
                        // We have to concat with the string after the column.
                        columnNameBuild.Append(concatColumnName.Replace(" ", ""))
                                .Append(" || ")
                                .Append("@ConCol")
                                .Append(columnName);
                    }
                    else
                    {
                        // We have to concat the Data with the Column data before the String to be concatanated with.
                        columnNameBuild.Append("@ConCol")
                            .Append(columnName)
                            .Append(" || ")
                            .Append(concatColumnName.Replace(" ", ""));
                    }
                    colBindObjs.Add("ConCol" + columnName, concatString.Trim());

                    // If the Table is a syncable table then we will Add this Column to the Column JSON.
                    try
                    {
                        // Here we will Add the Column to the JSON.
                        if (syncable)
                        {
                            // Create the Json Object.
                            JObject colJObj = new JObject
                            {
                                { "column", concatColumnName.Replace(" ", "") },
                                { "value", concatString },
                                { "type", "CONCAT" },
                                { "is_after_column", isAfterColumn }
                            };
                            // Store the Column JSON to the JSON Columns Object.
                            columnsJObj.Add(columnName, colJObj);
                        }
                    }
                    catch (Exception er)
                    {
                        // There was an Error.
                    }
                }
                return this;
            }
            catch (Exception e)
            {
                // There was an Error.
                isError = true;
                AddStatus("Add Column Error : " + e.Message);
                return this;
            }
        }

        // Set the Column to allow for math operations on the column. (Integer Numbers)
        public Update AddColumn(string columnName, string operationalColumnName, O operation, long operationValue)
        {
            try
            {
                // Here we get the Column Name and the Column Value to be added by the User.
                // Check if this is the Second Column and Add the comma.
                // Only let to proceed if the Column does'nt have the special rows.
                if (columnName.ElementAt(columnName.Length - 1) == '_')
                {
                    // This column is not a valid column and must be left only for the system to edit.
                    AddStatus("User cannot edit " + columnName + " System Column");
                }
                else
                {
                    // This Columns is valid.
                    if (columnNameBuild.Length > 0)
                    {
                        columnNameBuild.Append(", ");
                    }

                    // Add the Column to the Column Name Query string.
                    columnName = Regex.Replace(columnName, " ", "");
                    columnNameBuild.Append(Regex.Replace(columnName, " ", ""))
                        .Append(" = ")
                        .Append(Regex.Replace(operationalColumnName, " ", ""));

                    // Select the Right operator.
                    switch (operation)
                    {
                        case O.ADD:
                            columnNameBuild.Append('+');
                            break;
                        case O.SUBSCRACT:
                            columnNameBuild.Append('-');
                            break;
                        case O.MULTIPLY:
                            columnNameBuild.Append('*');
                            break;
                        case O.DIVIDE:
                            columnNameBuild.Append('/');
                            break;
                        case O.MODULAR:
                            columnNameBuild.Append('%');
                            break;
                        default:
                            return null;
                    }

                    // Add the Bindable string.
                    columnNameBuild.Append(" @C")
                        .Append(columnName)
                        .Append(' ');
                    colBindObjs.Add("C" + columnName, operationValue);

                    // If the Table is a syncable table then we will Add this Column to the Column JSON.
                    try
                    {
                        // Here we will Add the Column to the JSON.
                        if (syncable)
                        {
                            // Create the Json Object.
                            JObject colJObj = new JObject
                            {
                                { "code", operation.ToString() },
                                { "column", columnName },
                                { "value", operationValue },
                                { "type", "OPERATION" }
                            };
                            // Store the Column JSON to the JSON Columns Object.
                            columnsJObj.Add(columnName, colJObj);
                        }
                    }
                    catch (Exception er)
                    {
                        // There was an Error.
                    }
                }
                return this;
            }
            catch (Exception e)
            {
                // There was an Error.
                isError = true;
                AddStatus("Add Column Error : " + e.Message);
                return this;
            }
        }

        // Set the Column to allow for math operations on the column. (Floating point Numbers)
        public Update AddColumn(string columnName, string operationalColumnName, O operation, double operationValue)
        {
            try
            {
                // Here we get the Column Name and the Column Value to be added by the User.
                // Check if this is the Second Column and Add the comma.
                // Only let to proceed if the Column does'nt have the special rows.
                if (columnName.ElementAt(columnName.Length - 1) == '_')
                {
                    // This column is not a valid column and must be left only for the system to edit.
                    AddStatus("User cannot edit " + columnName + " System Column");
                }
                else
                {
                    // This Columns is valid.
                    if (columnNameBuild.Length > 0)
                    {
                        columnNameBuild.Append(", ");
                    }

                    // Add the Column to the Column Name Query string.
                    columnName = Regex.Replace(columnName, " ", "");
                    columnNameBuild.Append(Regex.Replace(columnName, " ", ""))
                        .Append(" = ")
                        .Append(Regex.Replace(operationalColumnName, " ", ""));

                    // Select the Right operator.
                    switch (operation)
                    {
                        case O.ADD:
                            columnNameBuild.Append('+');
                            break;
                        case O.SUBSCRACT:
                            columnNameBuild.Append('-');
                            break;
                        case O.MULTIPLY:
                            columnNameBuild.Append('*');
                            break;
                        case O.DIVIDE:
                            columnNameBuild.Append('/');
                            break;
                        case O.MODULAR:
                            columnNameBuild.Append('%');
                            break;
                        default:
                            return null;
                    }

                    // Add the Bindable string.
                    columnNameBuild.Append(" @C")
                        .Append(columnName)
                        .Append(' ');
                    colBindObjs.Add("C" + columnName, operationValue);

                    // If the Table is a syncable table then we will Add this Column to the Column JSON.
                    try
                    {
                        // Here we will Add the Column to the JSON.
                        if (syncable)
                        {
                            // Create the Json Object.
                            JObject colJObj = new JObject
                            {
                                { "code", operation.ToString() },
                                { "column", columnName },
                                { "value", operationValue },
                                { "type", "OPERATION" }
                            };
                            // Store the Column JSON to the JSON Columns Object.
                            columnsJObj.Add(columnName, colJObj);
                        }
                    }
                    catch (Exception er)
                    {
                        // There was an Error.
                    }
                }
                return this;
            }
            catch (Exception e)
            {
                // There was an Error.
                isError = true;
                AddStatus("Add Column Error : " + e.Message);
                return this;
            }
        }

        // The System Columns will be updated in the below method.
        private Update AddColumnInternal(ColumnData columnData)
        {
            try
            {
                // We will Add the Column using the internal method.
                if (columnNameBuild.Length > 0)
                {
                    columnNameBuild.Append(", ");
                }
                // Add the Column to the Column Name Query string.
                columnNameBuild.Append(columnData.GetColumn())
                    .Append(" = ")
                    .Append("@C")
                    .Append(columnData.GetColumn())
                    .Append(' ');
                colBindObjs.Add("C" + columnData.GetColumn(), columnData.Get());

                // If the Table is a syncable table then we will Add this Column to the Column JSON.
                try
                {
                    // Here we will Add the Column to the JSON.
                    if (syncable)
                    {
                        // Create the Json Object.
                        JObject colJObj = new JObject();
                        colJObj.Add("value", (long)columnData.Get());
                        colJObj.Add("type", "VALUE");
                        // Store the Column JSON to the JSON Columns Object.
                        columnsJObj.Add(columnData.GetColumn(), colJObj);
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
                // THere was an Error.
                isError = true;
                AddStatus("Add Column Error : " + e.Message);
                return this;
            }
        }

        // Set the Where Clauses needed to run the query.
        public Update AddWhere(Where clause)
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
                    .Append("@W")
                    .Append(clause.GetColumnName());
                whereBindObjs.Add("W" + clause.GetColumnName(), clause.GetColumnData());

                // Here we will add the Where to the Where JSON Object.
                try
                {
                    if (syncable)
                    {
                        // Here we will Add the Json Object.
                        JObject whereJObj = new JObject();
                        whereJObj.Add("name", clause.GetColumnName());
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
                    AddStatus("Where J Err : " + er.Message + " : " + er.StackTrace);
                }

                return this;
            }
            catch (Exception e)
            {
                // There was an Error.
                isError = true;
                AddStatus("Add Where Error : " + e.Message);
                return this;
            }
        }

        // Set the Role ID of the Row to be inserted.
        // Has to be a role of the user.
        public Update SetRoleID(long roleID)
        {
            try
            {
                // Here we will Add the Role ID given by the User to the Statement.
                // Check if the Table is syncable, then only Add the row level security.
                if (CloudDB.IsSyncable(tableName))
                {
                    // Lets validate if the User is allowed to write with this role.
                    if (cdb.ValidateRoleRLS(roleID, CloudDB.CRUD.UPDATE))
                    {
                        // The user is allowed to write with this role.
                        rlsType = C.RLS.ROW_RLS;
                        rlsID = roleID;
                        AddStatus("User is Authorized to Update in this Role");
                        return this;
                    }
                    else
                    {
                        // The user is not allowed to write with this role.
                        rlsType = C.RLS.NO_RLS;
                        rlsID = 0;
                        AddStatus("User is NOT Authorized to Update in this Role");
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
        public Update SetGroupID(long groupID)
        {
            try
            {
                // Here we will Add the Group ID given by the User to the Statement.
                // Check if the Table is syncable, then only Add the row level security.
                if (CloudDB.IsSyncable(tableName))
                {
                    // Lets validate if the User is allowed to write with this role.
                    if (cdb.ValidateGroupRLS(groupID, CloudDB.CRUD.UPDATE))
                    {
                        // The user is allowed to write with this role.
                        rlsType = C.RLS.GROUP_RLS;
                        rlsID = groupID;
                        AddStatus("User is Authorized to Update in this Group");
                        return this;
                    }
                    else
                    {
                        // The user is not allowed to write with this role.
                        rlsType = C.RLS.NO_RLS;
                        rlsID = 0;
                        isError = true;
                        AddStatus("User is NOT Authorized to Update in this Group");
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

        // Let the user to suggest which rls to add to in one method only.
        public Update SetRLSID(short rowLevelSecurityType, long rowLevelSecurityID)
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
                            if (cdb.ValidateRoleRLS(rowLevelSecurityID, CloudDB.CRUD.UPDATE))
                            {
                                // The user is allowed to write with this role.
                                rlsType = C.RLS.ROW_RLS;
                                rlsID = rowLevelSecurityID;
                                AddStatus("User is Authorized to Update in this Role");
                                return this;
                            }
                            else
                            {
                                // The user is not allowed to write with this role.
                                rlsType = C.RLS.NO_RLS;
                                rlsID = 0;
                                AddStatus("User is NOT Authorized to Update in this Role");
                                isError = true;
                                return this;
                            }
                        // Its a Group Based RLS.
                        case 2:
                            // Lets validate if the User is allowed to write with this role.
                            if (cdb.ValidateGroupRLS(rowLevelSecurityID, CloudDB.CRUD.UPDATE))
                            {
                                // The user is allowed to write with this role.
                                rlsType = C.RLS.GROUP_RLS;
                                rlsID = rowLevelSecurityID;
                                AddStatus("User is Authorized to Update in this Group");
                                return this;
                            }
                            else
                            {
                                // The user is not allowed to write with this role.
                                rlsType = C.RLS.NO_RLS;
                                rlsID = 0;
                                isError = true;
                                AddStatus("User is NOT Authorized to Update in this Group");
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
        public Update SetRowLevelSecurityID(short rowLevelSecurityType, long rowLevelSecurityID)
        {
            return SetRLSID(rowLevelSecurityType, rowLevelSecurityID);
        }


        // Here we will set syncable to false, if the user wants to.
        public Update SetSyncable(bool syncable)
        {
            this.syncable = syncable;
            return this;
        }

        // Set the Callback Listeners for the User.
        public Update SetOnDataUpdated(OnDataUpdated onDataUpdated)
        {
            this.onDataUpdated = onDataUpdated;
            return this;
        }

        // Here we will get the user related to this update.
        internal User GetUser()
        {
            try
            {
                // Here we will return the user related to this update.
                return cdb.IsSystemUser ? null : cdb.user;
            }
            catch (Exception e)
            {
                // There was an Error
                return null;
            }
        }

        // Set the update time.
        public Update SetUpdateTime(long updateTime)
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

        // Get the Update time.
        public long GetUpdateTime()
        {
            return updateTimeI;
        }

        // Allow the User to Get the SQL Statement Generated.
        public string GetSQL()
        {
            try
            {
                // Here we will Generate the SQL Statement.
                // Attach the Main Statement.
                // The update setup was successful
                // Check if the user is allowed to update.
                if (IsSuccessful())
                {
                    // Build the SQL Statement.
                    StringBuilder sqlBuilder = new StringBuilder();
                    sqlBuilder.Append("UPDATE ")
                        .Append(tableName)
                        .Append(" SET ")
                        .Append(columnNameBuild);

                    // Add the Where Clauses.
                    if (whereClauseBuilder.Length > 0)
                    {
                        sqlBuilder.Append(" WHERE ")
                            .Append(whereClauseBuilder);
                        string rlsWhere = cdb.RLSWhere(CloudDB.QueryType.UPDATE);
                        if (rlsWhere.Trim().Length > 0)
                        {
                            sqlBuilder.Append(" AND ")
                                .Append(rlsWhere);
                        }
                    }
                    else if (CloudDB.IsSyncable(tableName))
                    {
                        string rlsWhere = cdb.RLSWhere(CloudDB.QueryType.UPDATE);
                        if (rlsWhere.Trim().Length > 0)
                        {
                            sqlBuilder.Append(" WHERE ")
                                .Append(rlsWhere);
                        }
                    }

                    return sqlBuilder.ToString();
                }
                else
                {
                    // There was an Error in the Update.
                    return "Update Error : " + GetStatusTrace();
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                return "Error : " + e.Message;
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
                Thread updateExecutionThread = new Thread(InternalExecutor);
                // Start the Thread.
                updateExecutionThread.Start();
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
                // Lets Get the Rows of the Data that will be updated.
                List<Row> updatedSyncIDRows = new List<Row>();
                try
                {
                    // Here we will Get the Rows
                    // Lets get the updated rows.
                    // Lets Create the Statement to read from the updated table.
                    StringBuilder sqlBuilder = new StringBuilder();
                    sqlBuilder.Append("SELECT sync_id_ FROM ")
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
                    updatedSyncIDRows = CloudDB.SQLiteReaderToRows(sdr);
                    // Reset the Session.
                    readSess.Reset();
                    readSess.Dispose();
                }
                catch (Exception er)
                {
                    // There was an Error.
                }

                // Here we will Execute the Query.
                SQLiteCommand dbSess = cdb.GetSession();

                // Add the RLS Security into the system if user has given & if it is a syncable table.
                if (CloudDB.IsSyncable(tableName))
                {
                    // Lets handle the RLS Security of the System.
                    try
                    {
                        if (rlsID == 0)
                        {
                            // The RLS was not given.
                            AddStatus("RLS Not Provided");
                        }
                        else
                        {
                            // The RLS is Provided
                            AddWhere(new Where("rls_id_", Where.Type.EQUAL, rlsID));
                            AddWhere(new Where("rls_type_", Where.Type.EQUAL, rlsType));
                        }
                    }
                    catch (Exception er)
                    {
                        // There was an Error.
                    }

                    // Lets set the Update Time.
                    try
                    {
                        if (updateTimeI == 0)
                        {
                            // Lets Create an Update Time.
                            updateTimeI = Helper.CurrentTimeMillis();
                        }
                        AddColumnInternal(new ColumnData("update_time_", updateTimeI));
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
                        AddColumnInternal(new ColumnData("update_time_", updateTimeI));
                    }
                }

                // Lets create the Statement.
                dbSess.Reset();
                string sql = GetSQL();
                dbSess.CommandText = sql;
                // Bind the Column Params.
                foreach (string paramName in colBindObjs.Keys)
                {
                    // Bind the Params.
                    SQLiteParameter sqlParam = new SQLiteParameter(paramName, colBindObjs[paramName]);
                    // Add the Param to the Command.
                    dbSess.Parameters.Add(sqlParam);
                }
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
                // Lets Execute the Query.
                int updated = dbSess.ExecuteNonQuery();
                // Reset the Session if another similar query is to be run.
                dbSess.Reset();
                dbSess.Dispose();
                // If the data is to be synced with the server, we will sync it with the server.
                if (syncable && (updated > 0))
                {
                    // After we Execute the Update, lets get all the rows that have been updated.
                    List<Row> updatedRows = new List<Row>();
                    try
                    {
                        // Lets get the updated rows.
                        // Lets Create the Sync ID where in clause.
                        StringBuilder syncIDWhereINClauseBuilder = new StringBuilder();
                        foreach (Row syncIDRow in updatedSyncIDRows)
                        {
                            if (syncIDWhereINClauseBuilder.Length > 0)
                            {
                                syncIDWhereINClauseBuilder.Append(", ");
                            }
                            syncIDWhereINClauseBuilder.Append('"')
                                .Append(syncIDRow.GetString("sync_id_").Replace(" ", ""))
                                .Append('"');
                        }

                        // Lets Create the Statement to read from the updated table.
                        StringBuilder sqlBuilder = new StringBuilder();
                        sqlBuilder.Append("SELECT * FROM ")
                            .Append(tableName)
                            .Append(" WHERE sync_id_ IN ( ")
                            .Append(syncIDWhereINClauseBuilder)
                            .Append(" )");

                        // Lets Get the Session Object.
                        SQLiteCommand readSess = cdb.GetSession();
                        // Create the Statement.
                        readSess.CommandText = sqlBuilder.ToString();
                        // Lets Prepare the Statement.
                        readSess.Prepare();
                        // Lets execute the query.
                        SQLiteDataReader sdr = readSess.ExecuteReader();
                        // Lets read the data and add the data to the Rows List.
                        updatedRows = CloudDB.SQLiteReaderToRows(sdr);
                        // Reset the Session.
                        readSess.Reset();
                        readSess.Dispose();

                        // Lets send the response of the updated rows to the user.
                        // Send to the Receiver if the user has set a update result listener.
                        onDataUpdated?.Invoke(updatedRows);

                        // Lets just check if all the rows where updated successfully.
                        if (updatedRows.Count == updated)
                        {
                            // The update went successful.
                            AddStatus("All the Rows were updated Perfectly");
                        }
                        else
                        {
                            // The update has a problem.
                            AddStatus("Not all the rows got updated");
                        }
                    }
                    catch (Exception e)
                    {
                        // There was an Error.
                    }

                    // The data is to be synced with the Cloud Server.
                    CloudSync.Sync(this, updatedRows);
                }
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // Required Getters and Setters.
        internal JObject GetColumnJObj()
        {
            return columnsJObj;
        }

        internal JObject GetWhereJObj()
        {
            return wheresJObj;
        }

        internal Update SetSyncID(string syncID)
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

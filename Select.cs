using _36E_Business___ERP.helper;
using _36E_Business___ERP.security;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace _36E_Business___ERP.cloudDB
{
    /*
     * 
     * This class will allow the user to read from the database :
     * This will also maintain security protocols.
     * It will also Connect to the Local DB to read the required data.
     * It will also push the data to Cloud DB to be synced to the Online Cloud DB.
     * The Syncing will be handled by Cloud DB Sync.
     * 
     */
    class Select
    {
        // Implementation.
        /*
         new Select().From("table_name")
                .AddColumn("column_name")...
                .Where(Where clause)...
                .Join(Join phrase)...
                .SetDataChangedListener(OnDataChangedListener => ())
                .OnDataResult(OnDataResult => ())
                .Execute();
             */

        // Variables.
        private CloudDB cdb;
        private string status;
        private StringBuilder statusTrace;
        private string tableName;
        private StringBuilder columnNameBuilder;
        private StringBuilder joinsBuilder;
        private StringBuilder whereClauseBuilder;
        private StringBuilder orderByClauseBuilder;
        private Dictionary<string, object> bindObjs;
        private bool isError = false;
        private string objID;   // This will be the ID for this Cloud DB Instance.
        private List<Where> whereObjs;
        private List<string> joinTables;

        // Callbacks.
        // For the Initial Execution and Data from the database.
        public delegate void OnDataResult(List<Row> rows);
        private OnDataResult onDataResult = null;
        // For an Change in the database.
        public delegate void OnDataChanged(DataChange dataChange, Row row);
        private OnDataChanged onDataChangedListener = null;

        // Enums & Static Values.
        public enum DataChange
        {
            INSERTED, UPDATED, DELETED
        }

        public enum OrderType
        {
            ASC, DESC
        }


        // Default Constructor.
        // The Cloud DB Constructor
        public Select(CloudDB cdb)
        {
            try
            {
                // Default Constructor.
                // Lets initialize the Cloud DB Service, just in case.
                // Initialize the Cloud DB Service.
                // Lets Connect or create a Cloud DB instance.
                if (cdb.IsSystemUser || cdb.user.IsActive)
                {
                    this.cdb = cdb;     // Set the Cloud DB Service.
                    // We will have to allow the user to set the context as well.
                    // Initiate the required variables.
                    columnNameBuilder = new StringBuilder();
                    joinsBuilder = new StringBuilder();
                    whereClauseBuilder = new StringBuilder();
                    orderByClauseBuilder = new StringBuilder();
                    bindObjs = new Dictionary<string, object>();
                    whereObjs = new List<Where>();
                    joinTables = new List<string>();
                    //AddStatus("Initiated");
                }
                else
                {
                    // The user is not authenticated.
                    isError = false;
                    //AddStatus("User not Authenticated");
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                // AddStatus("Construction Err : " + e.Message);
            }
        }

        // The User token Constructor.
        public Select(string token)
        {
            try
            {
                // Default Constructor.
                // Lets initialize the Cloud DB Service, just in case.
                // Initialize the Cloud DB Service.
                // Lets Connect or create a Cloud DB instance.
                CloudDB cdb = new CloudDB(token);
                if (cdb.IsSystemUser || cdb.user.IsActive)
                {
                    this.cdb = cdb;     // Set the Cloud DB Service.
                    // We will have to allow the user to set the context as well.
                    // Initiate the required variables.
                    columnNameBuilder = new StringBuilder();
                    joinsBuilder = new StringBuilder();
                    whereClauseBuilder = new StringBuilder();
                    orderByClauseBuilder = new StringBuilder();
                    bindObjs = new Dictionary<string, object>();
                    whereObjs = new List<Where>();
                    joinTables = new List<string>();
                    //AddStatus("Initiated");
                }
                else
                {
                    // The user is not authenticated.
                    isError = false;
                    //AddStatus("User not Authenticated");
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                // AddStatus("Construction Err : " + e.Message);
            }
        }

        // The Method to also set the Table to be read.
        public Select From(string tableName)
        {
            try
            {
                // Check for authenticated.
                AddStatus("Going to Authenticate : " + tableName);
                if (this.tableName == null)
                {
                    if (cdb.ValidateCRUD(tableName, CloudDB.CRUD.READ))
                    {
                        AddStatus("User Authorized to Read from the Table");
                        // The user has provided
                        // We will remove all spaces and then add the table name.
                        // Authenticate the User against the Table.
                        this.tableName = Regex.Replace(tableName.Trim(), " ", "");
                        AddStatus("Set Table");
                        // The Security has been completed
                        return this;
                    }
                    else
                    {
                        // The user does'nt have authority to read from this table.
                        AddStatus("Not Authorized to Read Table");
                        isError = true;
                        return null;
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
                AddStatus(e.Message);
                this.tableName = null;
                isError = true;
                return null;
            }
        }

        // Here we will allow the system to add the table to be read from.
        internal Select FromSystem(string tableName)
        {
            try
            {
                // Check for authenticated.
                if (this.tableName == null && Helper.DBValidation.IsSystemTB(tableName) && cdb.IsSystemUser)
                {
                    // The user has provided
                    // We will remove all spaces and then add the table name.
                    // Authenticate the User against the Table.
                    this.tableName = Regex.Replace(tableName.Trim(), " ", "");
                    AddStatus("Set Table");
                    // The Security has been completed
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
                AddStatus(e.Message);
                this.tableName = null;
                isError = true;
                return null;
            }
        }

        // Get the Table Name.
        public string GetTableName()
        {
            return tableName;
        }

        // Set the Data Received Delegation Setter.
        internal Select SetOnDataResult(OnDataResult onDataResult)
        {
            this.onDataResult = onDataResult;
            return this;
        }

        // Set the Data Change Listener Delegation.
        internal Select SetOnDataChangedListener(OnDataChanged onDataChanged)
        {
            onDataChangedListener = onDataChanged;
            return this;
        }

        // Let the user to add the Columns to the Query.
        // Allow the user to add columns.
        public Select AddColumn(string columnName)
        {
            try
            {
                // Add a column name to the array.
                // Before adding to the view
                string columnNameN = Regex.Replace(columnName, " ", "");
                if (columnNameBuilder.Length > 0)
                {
                    columnNameBuilder.Append(", ");
                }
                columnNameBuilder.Append(columnNameN);
            }
            catch (Exception e)
            {
                // There was an Error.
            }
            return this;
        }

        // Allow the user to add columns and have an identifier set.
        public Select AddColumn(string columnName, string identifiedBy)
        {
            try
            {
                // Add a column name to the array.
                // Before adding to the view
                string columnNameN = Regex.Replace(columnName, " ", "")
                    + " AS " + Regex.Replace(identifiedBy, " ", "");
                if (columnNameBuilder.Length > 0)
                {
                    columnNameBuilder.Append(", ");
                }
                columnNameBuilder.Append(columnNameN);
            }
            catch (Exception e)
            {
                // There was an Error.
            }
            return this;
        }

        // Let the user to add JOINs and Join Multiple Tables in one Query.
        public Select AddJoin(Join.Type type, string jointTableName, string columnName, string joinOnColumnName)
        {
            try
            {
                // Here we will Join Multiple Tables.
                // Authorize if the User is allowed to Read from this table.
                if (cdb.ValidateCRUD(jointTableName, CloudDB.CRUD.READ))
                {
                    // We get the Join statements that the user wants to join.
                    StringBuilder joinSQL = new StringBuilder();
                    joinTables.Add(jointTableName);

                    // Build the Join SQL.
                    // Add the type of the Join.
                    switch (type)
                    {
                        case Join.Type.INNER_JOIN:
                            joinSQL.Append(" INNER JOIN ");
                            break;
                        case Join.Type.JOIN:
                            joinSQL.Append(" JOIN ");
                            break;
                        case Join.Type.OUTTER_JOIN:
                            joinSQL.Append(" OUTTER JOIN ");
                            break;
                        case Join.Type.LEFT_JOIN:
                            joinSQL.Append(" LEFT JOIN ");
                            break;
                        case Join.Type.RIGHT_JOIN:
                            joinSQL.Append(" RIGHT JOIN ");
                            break;
                        case Join.Type.FULL_JOIN:
                            joinSQL.Append(" FULL JOIN ");
                            break;
                    }
                    // Add the Table Name of the Join.
                    joinSQL.Append(jointTableName);
                    // Add the Column Name of the Join.
                    joinSQL.Append(" ON ")
                        .Append(tableName + '.' + columnName)
                        .Append('=')
                        .Append(jointTableName + '.' + joinOnColumnName);


                    // Lets Now Add the Join to the Select.
                    joinsBuilder.Append(joinSQL.ToString());
                    return this;
                }
                else
                {
                    // The user is not authorized to proceed.
                    return null;
                }
            }
            catch (Exception er)
            {
                // There was an Error.
                return null;
            }
        }

        // Let the System to add a Join over a system table.
        protected Select AddSystemJoin(Join join)
        {
            try
            {
                // Here we will add the join that the user is trying to add.
                // Validate the Join.
                joinsBuilder.Append(join.GetJoinSQL());
                return this;
            }
            catch (Exception e)
            {
                // There was an Error.
                return this;
            }
        }

        // Let the User to add Where clauses to the System.
        public Select AddWhere(Where clause)
        {
            try
            {
                // Add the where to the system, with the default connector.
                if (clause != null)
                {
                    if (whereClauseBuilder.Length > 0)
                    {
                        whereClauseBuilder.Append(clause.GetConnector());
                    }
                    if (clause.GetOperator() == " LIKE ")
                    {
                        // The User is adding a like clause.
                        whereClauseBuilder.Append("UPPER(" + clause.GetColumnName() + ")")
                            .Append(clause.GetOperator())
                            .Append('@')
                            .Append(clause.GetColumnName());
                        // Add the Params Object to the Dictionary
                        bindObjs.Add(clause.GetColumnName(), clause.GetColumnData().ToString().ToUpper() + "%");
                        // Add the Where clause to the list of where clauses.
                        whereObjs.Add(clause);
                    }
                    else
                    {
                        // The user is adding a normal compare operator.
                        whereClauseBuilder.Append(clause.GetColumnName())
                            .Append(clause.GetOperator())
                            .Append('@')
                            .Append(clause.GetColumnName());
                        // Add the Params Object to the Dictionary
                        bindObjs.Add(clause.GetColumnName(), clause.GetColumnData());
                        // Add the Where clause to the list of where clauses.
                        whereObjs.Add(clause);
                    }
                }
                return this;

            }
            catch (Exception e)
            {
                // There was an Error.
                AddStatus("Err : " + e.Message);
                return this;
            }
        }

        // Let the User to add an ordering clause to the system.
        // Order by using Asc.
        public Select AddOrderBy(string orderingColumn)
        {
            try
            {
                // Here we will add the ordering column.
                if (orderByClauseBuilder.Length > 0)
                {
                    orderByClauseBuilder.Append(", ");
                }
                orderByClauseBuilder.Append(orderingColumn);
                return this;
            }
            catch (Exception e)
            {
                // There was an Error.
                return this;
            }
        }

        // Order by using Asc / Desc.
        public Select AddOrderBy(string orderingColumn, OrderType orderBy)
        {
            try
            {
                // Here we will add the ordering column.
                if (orderByClauseBuilder.Length > 0)
                {
                    orderByClauseBuilder.Append(", ");
                }
                try
                {
                    switch (orderBy)
                    {
                        case OrderType.ASC:
                            orderByClauseBuilder.Append(orderingColumn)
                                .Append(" ASC ");
                            break;
                        case OrderType.DESC:
                            orderByClauseBuilder.Append(orderingColumn)
                                .Append(" DESC ");
                            break;
                        default:
                            orderByClauseBuilder.Append(orderingColumn);
                            break;
                    }
                }
                catch (Exception e)
                {
                    // There was an Error 
                    orderByClauseBuilder.Append(orderingColumn);
                }
                return this;
            }
            catch (Exception e)
            {
                // There was an Error.
                return this;
            }
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

        // Here we generate the SQL Statement for the User.
        private string GetSQL()
        {
            try
            {
                // Here we generate the SQL Statement.
                // Attach the Main Select statement.
                StringBuilder sqlBuilder = new StringBuilder();
                if (columnNameBuilder.Length == 0)
                {
                    columnNameBuilder.Append(" * ");
                }
                sqlBuilder.Append("SELECT ")
                    .Append(columnNameBuilder)
                    .Append(" FROM ")
                    .Append(tableName);

                // Add the Join.
                if (joinsBuilder.Length > 0)
                {
                    sqlBuilder.Append(joinsBuilder);
                }

                // Add the Where Clauses.
                if (whereClauseBuilder.Length > 0)
                {
                    sqlBuilder.Append(" WHERE ")
                        .Append(whereClauseBuilder);
                    if (CloudDB.IsSyncable(tableName))
                    {
                        if (joinTables.Count > 0)
                        {
                            // There are tables that are joint.
                            string rlsWhere = cdb.RLSWhere(CloudDB.QueryType.SELECT, tableName);
                            if (rlsWhere.Trim().Length > 0)
                            {
                                sqlBuilder.Append(" AND ")
                                    .Append(rlsWhere);
                            }

                            // Now Lets handle the Tables being Joint.
                            for (int joinTbIndex = 0; joinTbIndex < joinTables.Count; joinTbIndex++)
                            {
                                rlsWhere = cdb.RLSWhere(CloudDB.QueryType.SELECT, joinTables[joinTbIndex]);
                                if (rlsWhere.Trim().Length > 0)
                                {
                                    sqlBuilder.Append(" AND ")
                                        .Append(rlsWhere);
                                }
                            }
                        }
                        else
                        {
                            // No Tables have been Joined.
                            string rlsWhere = cdb.RLSWhere(CloudDB.QueryType.SELECT);
                            if (rlsWhere.Trim().Length > 0)
                            {
                                sqlBuilder.Append(" AND ")
                                    .Append(rlsWhere);
                            }
                        }
                    }
                }
                else if (CloudDB.IsSyncable(tableName))
                {
                    if (joinTables.Count > 0)
                    {
                        // There are Tables that are Joint.
                        string rlsWhere = cdb.RLSWhere(CloudDB.QueryType.SELECT, tableName);
                        if (rlsWhere.Trim().Length > 0)
                        {
                            sqlBuilder.Append(" WHERE ")
                                .Append(rlsWhere);
                        }

                        // Now Lets Handle the Tables being Joint.
                        for (int joinTbIndex = 0; joinTbIndex < joinTables.Count; joinTbIndex++)
                        {
                            rlsWhere = cdb.RLSWhere(CloudDB.QueryType.SELECT, joinTables[joinTbIndex]);
                            if (rlsWhere.Trim().Length > 0)
                            {
                                sqlBuilder.Append(" AND ")
                                    .Append(rlsWhere);
                            }
                        }
                    }
                    else
                    {
                        // There are Tables Being Joint.
                        string rlsWhere = cdb.RLSWhere(CloudDB.QueryType.SELECT);
                        if (rlsWhere.Trim().Length > 0)
                        {
                            sqlBuilder.Append(" WHERE ")
                                .Append(rlsWhere);
                        }
                    }
                }

                // Add the Order by Clause.
                if (orderByClauseBuilder.Length > 0)
                {
                    sqlBuilder.Append(" ORDER BY ")
                        .Append(orderByClauseBuilder);
                }

                return sqlBuilder.ToString();

            }
            catch (Exception e)
            {
                // There was an Error.
                return "Err : " + e.Message;
            }
        }

        // Here we will allow for the Execution of the Statement.
        // This will Execute in Synchronous Mode.
        public void Execute()
        {
            try
            {
                // Here we will Execute the Query in Synchronous mode.
                InternalExecution();
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // This will Execute in A-Synchronous Mode
        public void ExecuteAsync()
        {
            try
            {
                // Here we will Execute the Query in A-Synchronous mode.
                Thread executionThread = new Thread(InternalExecution);
                // Start the Thread.
                executionThread.Start();
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // This method will be used to execute the statement, but will only be called by another method in the class.
        private void InternalExecution()
        {
            try
            {
                // Set the Select Listener with Cloud Sync.
                CloudSync.SetSelectListener(this);
                // Here we will Execute the Query.
                // Reset the Session so it is clean.
                if (IsSuccessful())
                {
                    SQLiteCommand dbSess = cdb.GetSession();
                    dbSess.Reset();
                    // Create the SQL Statement.
                    string sql = GetSQL();
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
                    // Lets Execute the Query.
                    SQLiteDataReader sdr = dbSess.ExecuteReader();
                    // Check if the user is waiting for the result.
                    onDataResult?.Invoke(CloudDB.SQLiteReaderToRows(sdr));
                }
                else
                {
                    // The Select was not Successful.
                    onDataResult?.Invoke(new List<Row>());
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                AddStatus("Execution Err : " + e.StackTrace + " ::: " + e.Message);
                onDataResult?.Invoke(new List<Row>());
            }
        }

        // Generate a Random String if the Cloud DB Object ID has not been set.
        internal string GetIdentifier()
        {
            try
            {
                // Generate the Required ID if does'nt exist.
                objID = objID == null ? Security.GetSaltString(10) : objID;
                return objID;
            }
            catch (Exception e)
            {
                // There was an Error.
                return null;
            }
        }

        // Receive the data changed from the cloud sync in this method.
        // We will use this method to send the data to the user, of any data that was changed.
        internal void DataChanged(DataChange dataChange, Row row)
        {
            try
            {
                // Here we will send the data to the user if the user is listening to a change.
                // Lets Validate if the Row has to be sent to the listener.
                bool canProceed = false;
                // Now we will Split the Where Clauses according to AND & OR
                if (whereObjs.Count == 0)
                {
                    // There are no where clause, lets send the changed data to the user.
                    canProceed = true;
                }
                else if (whereObjs.Count == 1)
                {
                    // There is only one where clause.
                    // Lets process that, and then if it passes send it to the user.
                    try
                    {
                        Where whereObj = whereObjs[0];
                        switch (whereObj.operation)
                        {
                            case Where.Type.EQUAL:
                                // The Condition is Equals.
                                if (whereObj.GetColumnData().ToString().ToLower()
                                    .Equals(row.GetString(whereObj.GetColumnName()).ToLower()))
                                {
                                    // The row comes under the where condition.
                                    canProceed = true;
                                }
                                break;
                            case Where.Type.GREATER:
                                // The Condition is Greater.
                                if (row.GetBigInt(whereObj.GetColumnName()) > long.Parse(whereObj.GetColumnData().ToString()))
                                {
                                    // The row comes under the where condition.
                                    canProceed = true;
                                }
                                break;
                            case Where.Type.GREATER_EQUAL:
                                // The Condition is Greater.
                                if (row.GetBigInt(whereObj.GetColumnName()) >= long.Parse(whereObj.GetColumnData().ToString()))
                                {
                                    // The row comes under the where condition.
                                    canProceed = true;
                                }
                                break;
                            case Where.Type.LESSER:
                                // The Condition is Greater.
                                if (row.GetBigInt(whereObj.GetColumnName()) < long.Parse(whereObj.GetColumnData().ToString()))
                                {
                                    // The row comes under the where condition.
                                    canProceed = true;
                                }
                                break;
                            case Where.Type.LESSER_EQUAL:
                                // The Condition is Greater.
                                if (row.GetBigInt(whereObj.GetColumnName()) <= long.Parse(whereObj.GetColumnData().ToString()))
                                {
                                    // The row comes under the where condition.
                                    canProceed = true;
                                }
                                break;
                            default:
                                // There is no valid condition.
                                canProceed = false;
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        // There was an Error.
                    }
                }
                else
                {
                    // There are many where clauses, lets send do complex processing and send to the user if it passes.
                    List<Where> andWhereObjs = new List<Where>();
                    List<List<Where>> combOrWhereObjs = new List<List<Where>>();
                    for (int i = 0; i < whereObjs.Count; i++)
                    {
                        try
                        {
                            // Lets check which where clause is this.
                            if (i == 0)
                            {
                                // This is the first where clause.
                                // Add the where clause to the sectioning where.
                                andWhereObjs.Add(whereObjs[i]);
                            }
                            else
                            {
                                // This is after the first where clause.
                                // Lets now separate the where clauses.
                                if (whereObjs[i].GetConnector().Trim().ToUpper().Equals("AND"))
                                {
                                    // It is an And.
                                    // Add the where clause to the sectioning where.
                                    andWhereObjs.Add(whereObjs[i]);
                                }
                                else
                                {
                                    // Its an OR.
                                    // Add the Section Where Clauses to a combination of the where sections.
                                    combOrWhereObjs.Add(andWhereObjs);
                                    // Lets now clean the section where clause.
                                    andWhereObjs = new List<Where>();
                                    // Lets add this new clause to the section where clause.
                                    andWhereObjs.Add(whereObjs[i]);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            // There was an Error.
                        }
                    }
                    // Check if there is any where clauses in the where section.
                    if (andWhereObjs.Count > 0)
                    {
                        // There are where clauses in the where clause section.
                        combOrWhereObjs.Add(andWhereObjs);
                        // Clean where clause section.
                        andWhereObjs = new List<Where>();
                    }
                    // Now the Where Clause separation has been completed.
                    // Lets now validate the row according to the where clauses.
                    for (int i = 0; i < combOrWhereObjs.Count; i++)
                    {
                        // Lets test the Clauses within.
                        List<Where> secWheres = combOrWhereObjs[i];
                        bool isValid = false;
                        for (int j = 0; j < secWheres.Count; j++)
                        {
                            // Lets test the Conditions.
                            Where whereObj = secWheres[j];
                            switch (whereObj.operation)
                            {
                                case Where.Type.EQUAL:
                                    // The Condition is Equals.
                                    if (whereObj.GetColumnData().ToString().ToLower()
                                        .Equals(row.GetString(whereObj.GetColumnName()).ToLower()))
                                    {
                                        // The row comes under the where condition.
                                        isValid = true;
                                    }
                                    else
                                    {
                                        // The row fails the condition.
                                        isValid = false;
                                    }
                                    break;
                                case Where.Type.GREATER:
                                    // The Condition is Greater.
                                    if (row.GetBigInt(whereObj.GetColumnName()) > long.Parse(whereObj.GetColumnData().ToString()))
                                    {
                                        // The row comes under the where condition.
                                        isValid = true;
                                    }
                                    else
                                    {
                                        // The row fails the condition.
                                        isValid = false;
                                    }
                                    break;
                                case Where.Type.GREATER_EQUAL:
                                    // The Condition is Greater.
                                    if (row.GetBigInt(whereObj.GetColumnName()) >= long.Parse(whereObj.GetColumnData().ToString()))
                                    {
                                        // The row comes under the where condition.
                                        isValid = true;
                                    }
                                    else
                                    {
                                        // The row fails the condition.
                                        isValid = false;
                                    }
                                    break;
                                case Where.Type.LESSER:
                                    // The Condition is Greater.
                                    if (row.GetBigInt(whereObj.GetColumnName()) < long.Parse(whereObj.GetColumnData().ToString()))
                                    {
                                        // The row comes under the where condition.
                                        isValid = true;
                                    }
                                    else
                                    {
                                        // The row fails the condition.
                                        isValid = false;
                                    }
                                    break;
                                case Where.Type.LESSER_EQUAL:
                                    // The Condition is Greater.
                                    if (row.GetBigInt(whereObj.GetColumnName()) <= long.Parse(whereObj.GetColumnData().ToString()))
                                    {
                                        // The row comes under the where condition.
                                        isValid = true;
                                    }
                                    else
                                    {
                                        // The row fails the condition.
                                        isValid = false;
                                    }
                                    break;
                                default:
                                    // There is no valid condition.
                                    isValid = false;
                                    break;
                            }
                        }
                        if (isValid)
                        {
                            canProceed = true;
                            break;
                        }
                    }
                }
                // Lets proceed if the user is allowed.
                if (canProceed)
                {
                    // Lets send the data to the user.
                    onDataChangedListener?.Invoke(dataChange, row);
                }
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // Getter & Setter of the Status.
        public string GetStatus()
        {
            return status;
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

        // Get the Bind Objects.
        protected Dictionary<string, object> GetBindObjs()
        {
            // Get the Bind Objects.
            return bindObjs;
        }

        // Get if there is an Error.
        public bool IsSuccessful()
        {
            return !isError;
        }

    }
}
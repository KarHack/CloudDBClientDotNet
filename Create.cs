using System;
using System.Data.SQLite;
using System.Text;

namespace _36E_Business___ERP.cloudDB
{
    /*
     * 
     * This class will be used to Create the Tables and Indexes in the Database.
     * It will also Connect to the Local DB
     * 
     */
    internal class Create
    {
        // Implementation.
        /* 
         new Create()
                .Table("CREATE TABLE SQL")    OR      .Index("CREATE INDEX SQL")
                .Execute();
         */

        // Variables.
        private CloudDB cdb;
        private string status;
        private StringBuilder statusTrace;
        private bool isError = false;
        private string statement;

        // Constructor.
        public Create()
        {
            try
            {
                // Default Constructor.
                // Lets initialize the Cloud DB Service, just in case.
                // We will have to allow the user to set the context as well.
                // Initiate the required variables.
                AddStatus("Initiated");

                // Lets Connect or create a Cloud DB instance.
                cdb = new CloudDB(C.SYSTEM_USER_DB_PASSCODE);
            }
            catch (Exception e)
            {
                // There was an Error.
                AddStatus("Constructor Err : " + e.Message);
            }
        }

        // Required Methods.
        // Set the Table Statement to be Created.
        public Create Table(string statement)
        {
            try
            {
                // Set the Statement.
                this.statement = statement;
                return this;
            }
            catch (Exception e)
            {
                // There was an Error.
                return null;
            }
        }

        // Set the Index Statement to be Created
        public Create Index(string statement)
        {
            try
            {
                // Set the Statement.
                this.statement = statement;
                return this;
            }
            catch (Exception e)
            {
                // There was an Error.
                return null;
            }
        }

        // Execute the Query.
        public bool Execute()
        {
            try
            {
                // Lets now execute the query.
                // Here we will Execute the Query.
                if (statement != null)
                {
                    // Lets now run the Statement.
                    SQLiteCommand dbSess = cdb.GetSession();
                    // Reset the Session so it is clean.
                    dbSess.Reset();
                    // Create the SQL Statement.
                    Console.WriteLine("Statement : " + statement);
                    dbSess.CommandText = statement;
                    Console.WriteLine("Execution CommandText");
                    // Lets prepare the statement.
                    dbSess.Prepare();
                    Console.WriteLine("Execution Prepared");
                    // Lets Execute the Query.
                    int created = dbSess.ExecuteNonQuery();
                    Console.WriteLine("Execution Completed : " + created);
                    dbSess.Dispose();
                    Console.WriteLine("Execution Disposed");
                    AddStatus("Execution Completed");
                    return created >= 0;
                }
                else
                {
                    // The Statement was empty.
                    return false;
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                AddStatus("Execution Err : " + e.Message);
                Console.WriteLine("Execution Err : " + e.Message);
                return false;
            }
        }

        // Get the Status of the Statement
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

    }
}

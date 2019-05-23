using System;
using System.Text;
using System.Text.RegularExpressions;

namespace _36E_Business___ERP.cloudDB
{
    class Join
    {
        // Variables.
        private CloudDB cdb;
        private Type type;
        private string tableName;
        private string columnName;
        private string joinOnColumnName;
        private StringBuilder joinSQL;

        // Static values.
        public enum Type { INNER_JOIN, JOIN, OUTTER_JOIN, LEFT_JOIN, RIGHT_JOIN, FULL_JOIN }

        // Default Constructor.
        private Join()
        {
            // Not needed Default Constructor.
        }

        // This is the Constructor that we need.
        public Join(Type type, string tableName, string columnName, string joinOnColumnName)
        {
            try
            {
                // Authorize if the User is allowed to Read from this table.
                if (cdb.ValidateCRUD(tableName, CloudDB.CRUD.READ))
                {
                    // We get the Join statements that the user wants to join.
                    this.type = type;
                    this.columnName = Regex.Replace(columnName, " ", "");
                    this.joinOnColumnName = Regex.Replace(joinOnColumnName, " ", "");
                    this.tableName = Regex.Replace(tableName, " ", "");
                    joinSQL = new StringBuilder();

                    // Build the Join SQL.
                    // Add the type of the Join.
                    switch (type)
                    {
                        case Type.INNER_JOIN:
                            joinSQL.Append(" INNER JOIN ");
                            break;
                        case Type.JOIN:
                            joinSQL.Append(" JOIN ");
                            break;
                        case Type.OUTTER_JOIN:
                            joinSQL.Append(" OUTTER JOIN ");
                            break;
                        case Type.LEFT_JOIN:
                            joinSQL.Append(" LEFT JOIN ");
                            break;
                        case Type.RIGHT_JOIN:
                            joinSQL.Append(" RIGHT JOIN ");
                            break;
                        case Type.FULL_JOIN:
                            joinSQL.Append(" FULL JOIN ");
                            break;
                    }
                    // Add the Table Name of the Join.
                    joinSQL.Append(this.tableName);
                    // Add the Column Name of the Join.
                    joinSQL.Append(" ON ")
                        .Append(this.columnName)
                        .Append('=')
                        .Append(this.joinOnColumnName);
                }
                else
                {
                    // The user is not authorized to proceed.
                }
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // This will allow to JOIN with a system table.
        internal Join onSystem(Type type, string tableName, string columnName, string joinOnColumnName)
        {
            try
            {
                // We get the Join statements that the user wants to join.
                this.type = type;
                this.columnName = Regex.Replace(columnName, " ", "");
                this.joinOnColumnName = Regex.Replace(joinOnColumnName, " ", "");
                this.tableName = Regex.Replace(tableName, " ", "");
                joinSQL = new StringBuilder();

                // Build the Join SQL.
                // Add the type of the Join.
                switch (type)
                {
                    case Type.INNER_JOIN:
                        joinSQL.Append(" INNER JOIN ");
                        break;
                    case Type.JOIN:
                        joinSQL.Append(" JOIN ");
                        break;
                    case Type.OUTTER_JOIN:
                        joinSQL.Append(" OUTTER JOIN ");
                        break;
                    case Type.LEFT_JOIN:
                        joinSQL.Append(" LEFT JOIN ");
                        break;
                    case Type.RIGHT_JOIN:
                        joinSQL.Append(" RIGHT JOIN ");
                        break;
                    case Type.FULL_JOIN:
                        joinSQL.Append(" FULL JOIN ");
                        break;
                }
                // Add the Table Name of the Join.
                joinSQL.Append(this.tableName);
                // Add the Column Name of the Join.
                joinSQL.Append(" ON ")
                    .Append(this.columnName)
                    .Append('=')
                    .Append(this.joinOnColumnName);
                // Return the JOIN.
                return this;
            }
            catch (Exception e)
            {
                // There was an Error.
                return null;
            }
        }

        // Get the type of the Join.
        public Type GetType()
        {
            return type;
        }

        // Get the Column Name.
        public string GetColumnName()
        {
            return columnName;
        }

        // Get the table name.
        internal string GetTableName()
        {
            return tableName;
        }

        // Get the Join On Column Name.
        public string GetJoinOnColumnName()
        {
            return joinOnColumnName;
        }

        internal string GetJoinSQL()
        {
            return joinSQL.ToString();
        }

    }
}

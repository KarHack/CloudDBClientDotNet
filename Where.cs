using System;
using System.Text.RegularExpressions;

namespace _36E_Business___ERP.cloudDB
{
    class Where
    {
        // Variables.
        private string columnName;
        private object columnData;
        internal Type operation { get; set; }
        private bool andConnector;

        // Static Variables.
        public enum Type
        {
            EQUAL, LESSER, GREATER, LESSER_EQUAL, GREATER_EQUAL, LIKE
        }

        // Where Where
        public Where(string columnName, Type operation, object columnData)
        {
            // We add this to our private variables.
            this.columnName = Regex.Replace(columnName, " ", "");
            this.operation = operation;
            this.columnData = columnData;
            this.andConnector = true;
        }

        // Where that will allow the user to state the Connector Statement
        // Used to connect with the previous Where Where.
        public Where(bool andConnect, string columnName, Type operation, object columnData)
        {
            // We add this to our private variables.
            this.columnName = Regex.Replace(columnName, " ", "");
            this.operation = operation;
            this.columnData = columnData;
            this.andConnector = andConnect;
        }

        // Special Where constructor, only callable from the same package.
        protected Where()
        {
            // This is a simple constructor, and should not be used without knowing completly what to do.
        }

        // Here are the getters of the Class.
        // We get all the data from the object.
        public string GetColumnName()
        {
            return columnName;
        }

        public object GetColumnData()
        {
            return columnData;
        }

        public string GetOperator()
        {
            try
            {
                // Here we will return the Correct operand according to the selected.
                switch (operation)
                {
                    case Type.EQUAL:
                        return " = ";
                    case Type.GREATER:
                        return " > ";
                    case Type.GREATER_EQUAL:
                        return " >= ";
                    case Type.LESSER:
                        return " < ";
                    case Type.LESSER_EQUAL:
                        return " <= ";
                    case Type.LIKE:
                        return " LIKE ";
                    default:
                        return "";
                }
            } catch (Exception e)
            {
                // There was an Error.
                return "";
            }
        }

        internal Type GetOperatorType()
        {
            return operation;
        }

        internal string GetConnector()
        {
            return andConnector ? " AND " : " OR ";
        }

        internal void SetConnector(bool andConnect)
        {
            this.andConnector = andConnect;
        }
    }
}
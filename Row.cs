using System;
using System.Collections.Generic;
using System.Linq;

namespace _36E_Business___ERP.cloudDB
{
    /*
     * 
     * This class will be used to store the data of each row got from the database, or the sync class.
     * 
     */
    public class Row
    {
        // Variables.
        private Dictionary<string, object> columns;

        // Constructors.
        public Row()
        {
            // This is a default constructor.
            columns = new Dictionary<string, object>();
        }

        // Methods.
        // Add Columns.
        internal Row AddColumn(string columnName, object data)
        {
            try
            {
                // Add the Column.
                columns.Add(columnName, data);
                return this;
            }
            catch (Exception e)
            {
                // There was an Error.
                return null;
            }
        }

        // Get the Column Data required.
        public object GetValue(string columnName)
        {
            try
            {
                // Get the Correct column.
                if (columns.ContainsKey(columnName))
                {
                    // Column Exists. 
                    return columns[columnName];
                }
                else
                {
                    // Column Does'nt exist
                    return null;
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                return null;
            }
        }

        // Get the Column Data in Different Formats.
        // Get the Column Data in String.
        public string GetString(string columnName)
        {
            try
            {
                // Get the Correct column.
                if (columns.ContainsKey(columnName))
                {
                    // Column Exists. 
                    return columns[columnName].ToString();
                }
                else
                {
                    // Column Does'nt exist
                    return null;
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                return null;
            }
        }

        // Get the Column Data in Int.
        public int GetInt(string columnName)
        {
            try
            {
                // Get the Correct column.
                if (columns.ContainsKey(columnName))
                {
                    // Column Exists. 
                    return int.Parse(columns[columnName].ToString());
                }
                else
                {
                    // Column Does'nt exist
                    return -1;
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                return -1;
            }
        }

        // Get the Column Data in Small Int.
        public Int16 GetSmallInt(string columnName)
        {
            try
            {
                // Get the Correct column.
                if (columns.ContainsKey(columnName))
                {
                    // Column Exists. 
                    return Int16.Parse(columns[columnName].ToString());
                }
                else
                {
                    // Column Does'nt exist
                    return -1;
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                return -1;
            }
        }

        // Get the Column Data in Big Int.
        public Int64 GetBigInt(string columnName)
        {
            try
            {
                // Get the Correct column.
                if (columns.ContainsKey(columnName))
                {
                    // Column Exists. 
                    return Int64.Parse(columns[columnName].ToString());
                }
                else
                {
                    // Column Does'nt exist
                    return -1;
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                return -1;
            }
        }

        // Get the Column Data in Boolean.
        public bool GetBoolean(string columnName)
        {
            try
            {
                // Get the Correct column.
                if (columns.ContainsKey(columnName))
                {
                    // Column Exists. 
                    return bool.Parse(columns[columnName].ToString());
                }
                else
                {
                    // Column Does'nt exist
                    return false;
                }
            }
            catch (Exception e)
            {
                // There was an Error.
                return false;
            }
        }

        // Get the Column Names.
        public string[] GetColumnNames()
        {
            try
            {
                // Get all the column names and return to the data.
                string[] columnNames = new string[columns.Count];
                for (int i = 0; i < columns.Count; i++)
                {
                    columnNames[i] = columns.ElementAt(i).Key;
                }
                return columnNames;
            }
            catch (Exception e)
            {
                // There was an Error.
                return new string[0];
            }
        }

        // Check if the Column Name exists.
        public bool Contains(string columnName)
        {
            try
            {
                // Here we will check if the column name exists.
                return columns.ContainsKey(columnName);
            }
            catch (Exception er)
            {
                // There was an Error.
                return false;
            }
        }

    }
}

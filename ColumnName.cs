using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace _36E_Business___ERP.cloudDB
{
    class ColumnName
    {
        // Variables required.
        private List<string> columnNameArr;
        private StringBuilder columnNameBuilder;

        // Constructor.
        public ColumnName()
        {
            // This is a normal constructor.
            columnNameArr = new List<string>();
            columnNameBuilder = new StringBuilder();
        }

        // Get the array of the columns.
        public string[] GetColumnNames()
        {
            return columnNameArr.ToArray();
        }

        // Get the Columns in a string format.
        public string GetColumnSQL()
        {
            try
            {
                // Give the Column Names in a Simple string Format.
                return columnNameBuilder.ToString();

            }
            catch (Exception e)
            {
                // There was an Error.
                return " * ";
            }
        }

        // Allow the user to Add columns.
        public ColumnName Add(string columnName)
        {
            try
            {
                // Add a column name to the array.
                // Before adding to the view
                string columnNameN = Regex.Replace(columnName, " ", "");
                columnNameArr.Add(columnNameN);
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

        // Allow the user to Add columns and have an identifier set.
        public ColumnName Add(string columnName, string identifiedBy)
        {
            try
            {
                // Add a column name to the array.
                // Before adding to the view
                string columnNameN = Regex.Replace(columnName, " ", "")
                    + " AS " + Regex.Replace(identifiedBy, " ", "");
                columnNameArr.Add(columnNameN);
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

    }
}

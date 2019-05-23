using System.Text.RegularExpressions;

namespace _36E_Business___ERP.cloudDB
{
    class ColumnData
    {

        // Column data class object.
        // Variables.
        // Main object variable, will be filled by all, only used if needed.
        private object objData;

        // State Holder.
        private string column;

        // The Constructor.
        protected ColumnData()
        {
            // We don't require this.
        }

        // The usable Constructor.
        public ColumnData(string columnName)
        {
            this.column = Regex.Replace(columnName, " ", "");
        }

        // The Constructor to allow the Bind object and the Column Name to be put together.
        public ColumnData(string columnName, object columnData)
        {
            this.column = Regex.Replace(columnName, " ", "");
            this.objData = columnData;
        }

        // The object setters come here.
        public ColumnData Put(object columnData)
        {
            // This is the main object putter. Will be used as a non typing method.
            objData = columnData;
            return this;
        }

        // Make the Getter.    
        public object Get()
        {
            // Here we will return the main object.
            return objData;
        }

        public string GetColumn()
        {
            return column;
        }

    }

    class CD : ColumnData
    {
        public CD(string columnName) : base(columnName)
        {
        }

        public CD(string columnName, object columnData) : base(columnName, columnData)
        {
        }
    }
}
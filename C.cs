namespace _36E_Business___ERP.cloudDB
{
    class C
    {
        // Lets maintain all the IP Addresses and Required Variables.
        public const string CLOUD_DB_IP_ADDRESS = "35.221.25.213:8080";
        public const string A36E_BUSINESS_IP_ADDRESS = "35.221.25.213:8080";
        public const string CLOUD_PUSH_IP_ADDRESS = "35.188.246.244:8082";
        public const string CLOUD_DB = "CloudDB";
        public const string A36E_BUSINESS = "36eBusiness";
        public const string SYSTEM_USER_DB_PASSCODE = "KaranGoLucky";
        public const string VERSION_0_1 = "0.1";

        // These columns are present in all the tables to help maintain integrity of the system.
        public static class CommonColumns
        {

            // The columns that are common to all the tables.
            public const string UPDATE_TIME = "update_time_";

        }

        //Here We will Define all the Query Types,
        // Like SELECT, INSERT, UPDATE and DELETE.
        public enum QueryType
        {
            SELECT, INSERT, UPDATE, DELETE
        }

        public static class RLS
        {

            public const short NO_RLS = 0;
            public const short ROW_RLS = 1;
            public const short GROUP_RLS = 2;

        }

    }
}

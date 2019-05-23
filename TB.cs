using System;
using System.Threading;

namespace _36E_Business___ERP.cloudDB
{
    // This table will be used for all activities related to Tables.
    public static class TB
    {
        public static string Status { get; set; }

        // Here we will have all the Versions of the System Tables.
        internal static bool SetupSystemTable(int version)
        {
            try
            {
                // Lets start inserting the system tables.
                bool allTablesCreated = true;
                // Create the Tables Meta Table.
                // According to their versions.
                if (version == 1)
                {
                    // This will store and maintain all the tables.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS tables_ ("
                        + "id VARCHAR(32) PRIMARY KEY, "
                        + "name VARCHAR(50) NOT NULL, "
                        + "syncable BOOL NOT NULL DEFAULT false, "
                        + "offline_only BOOL NOT NULL DEFAULT false, "
                        + "online_only BOOL NOT NULL DEFAULT false, "
                        + "multi_tenant BOOL NOT NULL DEFAULT true, "
                        + "time_stamp DATETIME DEFAULT CURRENT_TIMESTAMP)").Execute();

                    // Create the Table to Maintain the Roles of the Tenant.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS roles_ ("
                        + "id BIGINT PRIMARY KEY, "
                        + "tenant_id BIGINT NOT NULL, "
                        + "name VARCHAR(16) NOT NULL UNIQUE, "
                        + "parent_id BIGINT NOT NULL DEFAULT 0, "
                        + "branch TEXT NOT NULL, "
                        + "time_stamp DATETIME DEFAULT CURRENT_TIMESTAMP)").Execute();

                    // Create an Index on the Roles Table.
                    new Create().Index("CREATE INDEX roles__parent_id_idx ON roles_(parent_id)").Execute();

                    // Create the Table to Maintain the Groups of the Tenant.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS groups_ ("
                        + "id BIGINT PRIMARY KEY, "
                        + "tenant_id BIGINT NOT NULL, "
                        + "name VARCHAR(16) NOT NULL UNIQUE, "
                        + "time_stamp DATETIME DEFAULT CURRENT_TIMESTAMP)").Execute();

                    // Create the Table to store the Data of the User.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS users_ ("
                        + "user_id UUID PRIMARY KEY, "
                        + "token VARCHAR(256) NOT NULL UNIQUE, "
                        + "push_token TEXT, "
                        + "tenant_id INTEGER NOT NULL, "
                        + "database_id VARCHAR(10) NOT NULL, "
                        + "device_id TEXT, "
                        + "device_uid VARCHAR(36), "
                        + "is_active BOOL NOT NULL DEFAULT false, "
                        + "last_logged_in DATETIME CURRENT_TIMESTAMP, "
                        + "time_stamp DATETIME DEFAULT CURRENT_TIMESTAMP)").Execute();

                    // Create the Table to Maintain the Table Level ACL.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS tables_acl_ ("
                        + "table_id VARCHAR(32) NOT NULL, "
                        + "user_id UUID NOT NULL, "
                        + "read TINYINT NOT NULL DEFAULT 0, "
                        + "write TINYINT NOT NULL DEFAULT 0, "
                        + "edit TINYINT NOT NULL DEFAULT 0, "
                        + "remove TINYINT NOT NULL DEFAULT 0, "
                        + "time_stamp DATETIME DEFAULT CURRENT_TIMESTAMP, "
                        + "PRIMARY KEY (table_id, user_id), "
                        //+ "CONSTRAINT user_fk FOREIGN KEY (user_id) REFERENCES users_(user_id), "
                        + "CONSTRAINT table_fk FOREIGN KEY (table_id) REFERENCES tables_(id))").Execute();

                    // Create the Table to Maintain the Role RLS.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS role_rls_ ("
                        + "role_id VARCHAR(32)  NOT NULL, "
                        + "user_id UUID NOT NULL, "
                        + "read TINYINT NOT NULL DEFAULT 0, "
                        + "write TINYINT NOT NULL DEFAULT 0, "
                        + "edit TINYINT NOT NULL DEFAULT 0, "
                        + "remove TINYINT NOT NULL DEFAULT 0, "
                        + "time_stamp DATETIME DEFAULT CURRENT_TIMESTAMP, "
                        + "PRIMARY KEY (role_id, user_id), "
                        + "CONSTRAINT role_fk FOREIGN KEY (role_id) REFERENCES roles_(id))").Execute();

                    // Create the Table to Maintain the Group RLS.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS group_rls_ ("
                        + "group_id VARCHAR(32) NOT NULL, "
                        + "user_id UUID NOT NULL, "
                        + "read TINYINT NOT NULL DEFAULT 0, "
                        + "write TINYINT NOT NULL DEFAULT 0, "
                        + "edit TINYINT NOT NULL DEFAULT 0, "
                        + "remove TINYINT NOT NULL DEFAULT 0, "
                        + "time_stamp DATETIME DEFAULT CURRENT_TIMESTAMP, "
                        + "PRIMARY KEY (group_id, user_id), "
                        + "CONSTRAINT group_fk FOREIGN KEY (group_id) REFERENCES groups_(id))").Execute();

                    // Create the Table to Store all the Data Inserted into the Database while offline.
                    // This will hold the data when syncing with the server.
                    // The data written offline on this device, will use this table to push the data to the server.
                    // As soon as the data is pushed to the server, the row will be deleted from this table.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS sync_offline_ ("
                        + "id UUID, "    // This will be a (GUID) VARCHAR(36) saved.
                        + "crud_type VARCHAR(16) NOT NULL CHECK (crud_type = 'INSERT' "
                        + "OR crud_type = 'UPDATE' OR crud_type = 'DELETE'), "
                        + "table_name VARCHAR(32) NOT NULL, "
                        + "data TEXT NOT NULL, "    // This will be all the params sent during the data entry to the server
                        + "token TEXT NOT NULL, "
                        + "update_time_ INTEGER DEFAULT (strftime('%s','now') * 1000), "
                        + "time_stamp DATETIME DEFAULT CURRENT_TIMESTAMP)").Execute();

                    allTablesCreated = new Create().Index("CREATE INDEX sync_offline_id_idx_ ON sync_offline_(id)").Execute();

                    // Create a Table that stores all the data that is synced with the database.
                    // This table will be used to store all the data that is pushed to this device.
                    // So that we can stop overlapping data entries.
                    // This will be all the data sent by server and also this device on every data change.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS synced_check_ ("
                        + "id UUID, "    // This will be a (GUID) VARCHAR(36) saved.
                        + "crud_type VARCHAR(16) NOT NULL CHECK (crud_type = 'INSERT' OR crud_type = 'UPDATE' OR crud_type = 'DELETE'), "
                        + "table_name VARCHAR(32) NOT NULL, "
                        + "data TEXT NOT NULL, "    // This will be all the params sent during the data entry by the server
                        + "is_self_written BOOL NOT NULL DEFAULT true, "
                        + "update_time_ INTEGER DEFAULT (strftime('%s','now') * 1000), "
                        + "time_stamp DATETIME DEFAULT CURRENT_TIMESTAMP)").Execute();

                    allTablesCreated = new Create().Index("CREATE INDEX synced_check_id_idx_ ON synced_check_(id)").Execute();
                    /*
                    // Create a table that will store all the API Calls.
                    // The Data will be inserted when the api call will be made.
                    // And will be deleted when the api call returns.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS running_sync_calls_ ("
                        + "sync_offline_id INTEGER PRIMARY KEY, "
                        + "endpoint TEXT NOT NULL, "
                        + "params TEXT NOT NULL, "
                        + "checksum VARCHAR(64) NOT NULL, "  // This will be the Checksum of 
                                                             // -> Endpoint, Params, ID of the Row of Sync Offline
                        + "time_stamp DATETIME DEFAULT CURRENT_TIMESTAMP, "
                        + "FOREIGN KEY (sync_offline_id) REFERENCES sync_offline_(id))").Execute();
                    */
                    // Create the table to maintain all the updates and versions of the database and software.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS updates_ ("
                        + "id INTEGER PRIMARY KEY DEFAULT 1, "  // We will always update it into 1 ID only.
                        + "software_version FLOAT NOT NULL, "
                        + "software_version_name TEXT NOT NULL, "
                        + "database_version INTEGER NOT NULL, "
                        + "update_release_date TEXT NOT NULL, "
                        + "time_stamp DATETIME DEFAULT CURRENT_TIMESTAMP)").Execute();

                }
                return allTablesCreated;
            }
            catch (Exception e)
            {
                // There was an Error.
                return false;
            }
        }

        // Here we can have all the versions of the tables.
        // This Method will create all the initial tables required for 36E Business.
        internal static bool SetupTablesV1()
        {
            try
            {
                bool allTablesCreated = true;

                // Initialize the Required DB Connect.
                try
                {
                    // Create the Table to Maintain the Countries.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS countries ( "
                            + "id INTEGER PRIMARY KEY, "
                            + "rls_id_ INTEGER NOT NULL DEFAULT 0, "
                            + "rls_type_ TINYINT NOT NULL DEFAULT 0, "
                            + "sync_id_ UUID NOT NULL UNIQUE,"
                            + "name VARCHAR(50) NOT NULL UNIQUE, "
                            + "code_num TINYINT NOT NULL UNIQUE, "
                            + "code_char VARCHAR(3) NOT NULL UNIQUE, "
                            + "currency VARCHAR(16) NOT NULL, "
                            + "flag_ico VARCHAR DEFAULT NULL, "
                            + "status VARCHAR(16) DEFAULT NULL, "
                            + "special_message TEXT, "
                            + "operations_start_date DATE CURRENT_DATE, "
                            + "time_stamp DATETIME DEFAULT CURRENT_TIMESTAMP,"
                            + "update_time_ INTEGER DEFAULT (strftime('%s','now') * 1000))").Execute();

                    // Create the Table to Maintain the States.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS states ( "
                            + "id INTEGER PRIMARY KEY, "
                            + "rls_id_ INTEGER NOT NULL DEFAULT 0, "
                            + "rls_type_ TINYINT NOT NULL DEFAULT 0, "
                            + "sync_id_ UUID NOT NULL UNIQUE,"
                            + "name VARCHAR(50) NOT NULL UNIQUE, "
                            + "gst_code TINYINT NOT NULL UNIQUE, "
                            + "code VARCHAR(4) NOT NULL UNIQUE, "
                            + "country_id INTEGER NOT NULL, "
                            + "time_stamp DATETIME DEFAULT CURRENT_TIMESTAMP, "
                            + "update_time_ INTEGER DEFAULT (strftime('%s','now') * 1000))").Execute();

                    // Create the Table to Maintain the Company Details.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS companies ("
                        + "id INTEGER PRIMARY KEY, "
                        + "rls_id_ INTEGER NOT NULL DEFAULT 0, "
                        + "rls_type_ TINYINT NOT NULL DEFAULT 0, "
                        + "sync_id_ UUID NOT NULL UNIQUE,"
                        + "account_id INTEGER NOT NULL, "
                        + "gst_in VARCHAR(15) CONSTRAINT valid_gst CHECK (LENGTH(gst_in) = 15 OR NULL), "   // GST Validation should be carried out by Code.
                        + "pan_num VARCHAR(10) CONSTRAINT valid_pan_num CHECK (LENGTH(pan_num) = 10 OR NULL), " // PAN Num Validation should be carried out by Code.
                        + "code VARCHAR(5) NOT NULL CONSTRAINT valid_code CHECK(LENGTH(code) = 5), "    // COMPANY CODE Validation should be carried out by Code.
                        + "name VARCHAR(100) NOT NULL CONSTRAINT valid_name CHECK(LENGTH(name) <= 100), "
                        + "alias VARCHAR(50) NOT NULL CONSTRAINT valid_alias CHECK(LENGTH(alias) <= 50), "
                        + "owner_name VARCHAR(50) NOT NULL, "
                        + "state_id INTEGER NOT NULL, "
                        + "city VARCHAR(50) NOT NULL, "
                        + "email_id VARCHAR(50) NOT NULL CONSTRAINT valid_email_id CHECK(email_id LIKE '%_@__%.__%' AND email_id = LOWER(email_id) AND LENGTH(email_id) <= 50), "
                        + "phone_number BIGINT NOT NULL CONSTRAINT valid_phone_number CHECK(LENGTH(phone_number) BETWEEN 8 AND 12), "
                        + "landline_number BIGINT CONSTRAINT valid_landline_number CHECK(LENGTH(landline_number) BETWEEN 8 AND 12 OR landline_number = 0), "
                        + "country_id INTEGER NOT NULL, "
                        + "time_stamp DATETIME DEFAULT CURRENT_TIMESTAMP, "
                        + "update_time_ INTEGER DEFAULT (strftime('%s','now') * 1000))").Execute();

                    //allTablesCreated = new Create().Index("CREATE UNIQUE INDEX companies_sync_id_idx_ ON companies(sync_id_)").Execute();

                    // Create the Table to Maintain the Company Billing Address.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS company_billing_address (" +
                        "company_id INTEGER PRIMARY KEY," +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0," +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0," +
                        "sync_id_ UUID NOT NULL UNIQUE," +
                        "address_name VARCHAR(50) NOT NULL," +
                        "address_line1 VARCHAR(50) NOT NULL," +
                        "address_line2 VARCHAR(50) NOT NULL," +
                        "address_line3 VARCHAR(50) NOT NULL," +
                        "city VARCHAR(50) NOT NULL," +
                        "state_id INTEGER NOT NULL," +
                        "country_id INTEGER NOT NULL," +
                        "time_stamp DATETIME DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT (strftime('%s','now') * 1000))").Execute();

                    // Create the Table to maintain the Office Addresses of the Companies.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS office_addresses ("
                        + "id INTEGER PRIMARY KEY AUTOINCREMENT, "
                        + "tenant_id_ INTEGER NOT NULL DEFAULT 0, "
                        + "rls_id_ INTEGER NOT NULL DEFAULT 0, "
                        + "rls_type_ TINYINT NOT NULL DEFAULT 0, "
                        + "sync_id_ UUID NOT NULL UNIQUE,"
                        + "name VARCHAR(50) NOT NULL, "
                        + "state_id INTEGER NOT NULL, "
                        + "line1 VARCHAR(100) NOT NULL, "
                        + "line2 VARCHAR(100) NOT NULL, "
                        + "line3 VARCHAR(100) NOT NULL, "
                        + "pincode VARCHAR(10) NOT NULL, "
                        + "city VARCHAR(50) NOT NULL, "
                        + "email_id VARCHAR(50) NOT NULL CONSTRAINT valid_email_id CHECK (email_id LIKE '%_@__%.__%' AND email_id = LOWER(email_id) AND LENGTH(email_id) <= 50), "
                        + "phone_number BIGINT NOT NULL CONSTRAINT valid_phone_number CHECK (LENGTH(phone_number) BETWEEN 8 AND 12), "
                        + "lt DECIMAL(8, 8) NOT NULL DEFAULT 0, "
                        + "ln DECIMAL(8, 8) NOT NULL DEFAULT 0, "
                        + "is_hq BOOL NOT NULL DEFAULT FALSE, "
                        + "status VARCHAR(36) NOT NULL CONSTRAINT valid_status CHECK (status IN ('ACTIVE', 'IN PROGRESS', 'TEMPORARY SHUT DOWN', 'SHUT DOWN')), "
                        + "operations_start_time DATE CURRENT_DATE, "
                        + "time_stamp DATETIME DEFAULT CURRENT_TIMESTAMP, "
                        + "update_time_ INTEGER DEFAULT (strftime('%s','now') * 1000))").Execute();

                    // Create the Indexes of the Office Addresses.
                    /*allTablesCreated = new Create().Index("CREATE INDEX office_addresses_lt_idx ON office_addresses(lt)").Execute();
                    allTablesCreated = new Create().Index("CREATE INDEX office_addresses_ln_idx ON office_addresses(ln)").Execute();
                    allTablesCreated = new Create().Index("CREATE UNIQUE INDEX office_addresses_sync_id_idx_ ON office_addresses(sync_id_)").Execute();*/

                    // Create the Features Table of the System.
                    /* All the Features will Come here. */
                    /* The Access Hierarchy Below :
                        No Access = 0,
                        Read only = 1,
                        Edit with Read = 2,
                        Write with Above = 3,
                        Delete & Full Access = 4,
                    */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS features ("
                        + "id INTEGER PRIMARY KEY AUTOINCREMENT, "
                        + "rls_id_ INTEGER NOT NULL DEFAULT 0, "
                        + "rls_type_ TINYINT NOT NULL DEFAULT 0, "
                        + "sync_id_ UUID NOT NULL UNIQUE, "
                        + "name VARCHAR(50) NOT NULL, "
                        + "description VARCHAR(150) NOT NULL, "
                        + "rls_type TINYINT NOT NULL DEFAULT 0, "
                        + "access_hierarchy TINYINT NOT NULL DEFAULT 0, "
                        + "feature_group VARCHAR(32) NOT NULL, "
                        + "feature_sub_group VARCHAR(32) NOT NULL, "
                        + "update_time_ INTEGER DEFAULT (strftime('%s','now') * 1000), "
                        + "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP)").Execute();

                    // Create the Features Table Map of the System.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS features_table_map ("
                        + "feature_id INTEGER NOT NULL, "
                        + "table_id VARCHAR(32) NOT NULL, "
                        + "rls_id_ INTEGER NOT NULL DEFAULT 0, "
                        + "rls_type_ TINYINT NOT NULL DEFAULT 0, "
                        + "sync_id_ UUID NOT NULL UNIQUE, "
                        + "read TINYINT DEFAULT 0, "
                        + "write TINYINT DEFAULT 0, "
                        + "edit TINYINT DEFAULT 0, "
                        + "remove TINYINT DEFAULT 0, "
                        + "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, "
                        + "update_time_ INTEGER DEFAULT (strftime('%s','now') * 1000), "
                        + "PRIMARY KEY(feature_id, table_id), "
                        + "CONSTRAINT feature_fk FOREIGN KEY (feature_id) REFERENCES features(id))").Execute();

                    //allTablesCreated = new Create().Index("CREATE UNIQUE INDEX features_map_sync_id_idx_ ON features_table_map(sync_id_)").Execute();

                    // Create the table to Maintain all the Users in a Company.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS users ("
                        + "user_id UUID PRIMARY KEY, "
                        + "tenant_id_ INTEGER NOT NULL DEFAULT 0, "
                        + "rls_id_ INTEGER NOT NULL DEFAULT 0, "
                        + "rls_type_ TINYINT NOT NULL DEFAULT 0, "
                        + "sync_id_ UUID NOT NULL UNIQUE, "
                        + "first_name VARCHAR(50) NOT NULL, "
                        + "last_name VARCHAR(50) NOT NULL, "
                        + "email_id VARCHAR(50) NOT NULL CONSTRAINT valid_email_id CHECK (email_id LIKE '%_@__%.__%' AND email_id = LOWER(email_id) AND LENGTH(email_id) <= 50), "
                        + "phone_number BIGINT NOT NULL CONSTRAINT valid_phone_number CHECK (LENGTH(phone_number) BETWEEN 8 AND 12), "
                        + "full_code VARCHAR(12) NOT NULL UNIQUE CONSTRAINT valid_full_code CHECK (LENGTH(full_code) = 12), "
                        + "password VARCHAR(64) NOT NULL CONSTRAINT valid_password CHECK (LENGTH(password) = 64), "
                        + "created_user_id UUID NOT NULL, "
                        + "user_token VARCHAR(160) NOT NULL CONSTRAINT valid_user_token CHECK (LENGTH(user_token) = 160), "
                        + "locked_down BOOL NOT NULL DEFAULT false, "
                        + "last_logged_in DATETIME DEFAULT CURRENT_TIMESTAMP, "
                        + "time_stamp DATETIME DEFAULT CURRENT_TIMESTAMP, "
                        + "update_time_ INTEGER DEFAULT (strftime('%s','now') * 1000))").Execute();

                    //allTablesCreated = new Create().Index("CREATE UNIQUE INDEX users_sync_id_idx_ ON users(sync_id_)").Execute();

                    // Create the Features User RLS Map of the System.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS feature_user_rls_map ( "
                        + "user_id UUID NOT NULL, "
                        + "feature_id INTEGER NOT NULL, "
                        + "tenant_id_ INTEGER NOT NULL DEFAULT 0, "
                        + "rls_id_ INTEGER NOT NULL DEFAULT 0, "
                        + "rls_type_ TINYINT NOT NULL DEFAULT 0, "
                        + "sync_id_ UUID NOT NULL UNIQUE, "
                        + "name VARCHAR(50), "
                        + "description VARCHAR(50), "
                        + "access_level TINYINT NOT NULL DEFAULT 0 CONSTRAINT valid_access_level CHECK (access_level BETWEEN 0 AND 4), "
                        + "cdb_rls_id INTEGER NOT NULL DEFAULT 0, "
                        + "cdb_rls_type TINYINT NOT NULL DEFAULT 0, "
                        + "begin_time TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP, "
                        + "expiry_time TIMESTAMP NOT NULL, "
                        + "is_active BOOL NOT NULL DEFAULT false, "
                        + "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, "
                        + "update_time_ INTEGER DEFAULT (strftime('%s','now') * 1000), "
                        + "PRIMARY KEY(feature_id, tenant_id_), "
                        + "CONSTRAINT user_fk FOREIGN KEY (user_id) REFERENCES users(user_id), "
                        + "CONSTRAINT feature_fk FOREIGN KEY (feature_id) REFERENCES features(id))").Execute();

                    //allTablesCreated = new Create().Index("CREATE UNIQUE INDEX features_user_rls_map_sync_id_idx_ ON feature_user_rls_map(sync_id_)").Execute();


                    // Now the Main Functionality of the System starts here.
                    // Create the CONSUMER Tables.
                    // Create the Customer Detail's Table.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS consumers (" +
                        "consumer_id UUID PRIMARY KEY," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "first_name VARCHAR(50) NOT NULL," +
                        "middle_name VARCHAR(50), " +
                        "last_name VARCHAR(50), " +
                        "email_id VARCHAR(50) CONSTRAINT valid_email_id CHECK ((email_id LIKE '%_@__%.__%' AND email_id = LOWER(email_id) AND LENGTH(email_id) <= 50) OR LENGTH(email_id) = 0 OR NULL), " +
                        "phone_number BIGINT NOT NULL CONSTRAINT valid_phone_number CHECK (LENGTH(phone_number) BETWEEN 8 AND 12), " +
                        "gst_in VARCHAR(15) CONSTRAINT valid_gst CHECK (LENGTH(gst_in) = 15 OR NULL), " + // GST Validation should be carried out by Code. 
                        "city VARCHAR(50) NOT NULL," +
                        "state_id INTEGER NOT NULL," +
                        "other_details TEXT," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT (strftime('%s','now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY (tenant_id_) REFERENCES companies(id), " +
                        "CONSTRAINT state_fk FOREIGN KEY (state_id) REFERENCES states(id))").Execute();

                    // Create the Consumer's Billing Details (In Case of GST Number to be added)
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS consumer_billing (" +
                        "consumer_id UUID PRIMARY KEY, " +
                        "gst_in VARCHAR(15) CONSTRAINT valid_gst CHECK(LENGTH(gst_in) = 15 OR NULL)," + /* GST Validation should be carried out by Code. */
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "company_name VARCHAR(100) NOT NULL, " +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT (strftime('%s','now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY (tenant_id_) REFERENCES companies(id), " +
                        "CONSTRAINT consumer_fk FOREIGN KEY(consumer_id) REFERENCES consumers(consumer_id))").Execute();

                    // Create the Consumer's Addresses.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS consumer_addresses (" +
                        "address_id UUID PRIMARY KEY, " +
                        "consumer_id UUID NOT NULL, " +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "address_name VARCHAR(50)," +
                        "addressed_to_name VARCHAR(50)," +
                        "is_default BOOL NOT NULL DEFAULT false, " +
                        "line1 VARCHAR(100) NOT NULL, " +
                        "line2 VARCHAR(100), " +
                        "line3 VARCHAR(100), " +
                        "landmark VARCHAR(100), " +
                        "pincode VARCHAR(10) NOT NULL, " +
                        "city VARCHAR(50) NOT NULL, " +
                        "state_id INTEGER NOT NULL, " +
                        "state_name VARCHAR(50), " +
                        "email_id VARCHAR(50) CONSTRAINT valid_email_id CHECK ((email_id LIKE '%_@__%.__%' AND email_id = LOWER(email_id) AND LENGTH(email_id) <= 50) OR LENGTH(email_id) = 0 OR NULL),  " +
                        "phone_number BIGINT CONSTRAINT valid_phone_number CHECK ((LENGTH(phone_number) BETWEEN 8 AND 12) OR phone_number = 0), " +
                        "address_type VARCHAR(32) NOT NULL CONSTRAINT valid_address_type CHECK (address_type IN ('HOME', 'OFFICE', 'OTHER')), " +
                        "other_details TEXT, " +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT (strftime('%s','now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY (tenant_id_) REFERENCES companies(id), " +
                        "CONSTRAINT state_fk FOREIGN KEY (state_id) REFERENCES states(id), " +
                        "CONSTRAINT consumer_fk FOREIGN KEY (consumer_id) REFERENCES consumers(consumer_id))").Execute();

                    // Create the Buyer's Details. This is Mainly for Full & Small Invoice Types, Like B2B.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS buyers (" +
                        "buyer_id UUID PRIMARY KEY, " +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "buyer_name VARCHAR(50) NOT NULL," +
                        "buyer_alias VARCHAR(50) NOT NULL," +
                        "gst_in VARCHAR(15) CONSTRAINT valid_gst CHECK(LENGTH(gst_in) = 15 OR NULL)," + /* GST Validation should be carried out by Code. */
                        "pan_num VARCHAR(10) CONSTRAINT valid_pan_num CHECK(LENGTH(pan_num) = 10 OR NULL)," +
                        "email_id VARCHAR(50) CONSTRAINT valid_email_id CHECK ((email_id LIKE '%_@__%.__%' AND email_id = LOWER(email_id) AND LENGTH(email_id) <= 50) OR LENGTH(email_id) = 0 OR NULL), " +
                        "phone_number BIGINT NOT NULL CONSTRAINT valid_phone_number CHECK (LENGTH(phone_number) BETWEEN 8 AND 12), " +
                        "city VARCHAR(50) NOT NULL," +
                        "state_id INTEGER NOT NULL," +
                        "other_details TEXT," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s', 'now') * 1000)," +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id)," +
                        "CONSTRAINT state_fk FOREIGN KEY(state_id) REFERENCES states(id))").Execute();

                    // Create the Buyer's Address Table.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS buyer_addresses (" +
                        "address_id UUID PRIMARY KEY, " +
                        "buyer_id UUID NOT NULL," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0," +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "address_name VARCHAR(50)," +
                        "addressed_to_name VARCHAR(50)," +
                        "gst_in VARCHAR(15) CONSTRAINT valid_gst CHECK(LENGTH(gst_in) = 15 OR NULL), " + /* GST Validation should be carried out by Code. */
                        "is_default BOOL NOT NULL DEFAULT false, " +
                        "line1 VARCHAR(100) NOT NULL," +
                        "line2 VARCHAR(100), " +
                        "line3 VARCHAR(100), " +
                        "landmark VARCHAR(100), " +
                        "pincode VARCHAR(10) NOT NULL," +
                        "city VARCHAR(50) NOT NULL," +
                        "state_id INTEGER NOT NULL, " +
                        "state_name VARCHAR(50), " +
                        "email_id VARCHAR(50) CONSTRAINT valid_email_id CHECK ((email_id LIKE '%_@__%.__%' AND email_id = LOWER(email_id) AND LENGTH(email_id) <= 50) OR LENGTH(email_id) = 0 OR NULL), " +
                        "phone_number BIGINT CONSTRAINT valid_phone_number CHECK ((LENGTH(phone_number) BETWEEN 8 AND 12) OR phone_number = 0), " +
                        "address_type VARCHAR(32) NOT NULL CONSTRAINT valid_address_type CHECK(address_type IN('HOME', 'OFFICE', 'OTHER')), " +
                        "other_details TEXT," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s', 'now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id), " +
                        "CONSTRAINT state_fk FOREIGN KEY(state_id) REFERENCES states(id), " +
                        "CONSTRAINT buyer_fk FOREIGN KEY(buyer_id) REFERENCES buyers(buyer_id))").Execute();


                    /* Create the Product Related Tables. All the Products & Related Information will be stored in these tables. */
                    /* Store the Categories & Sub Categories of All the Products. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS product_categories( " +
                        "category_id INTEGER PRIMARY KEY AUTOINCREMENT," +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "name VARCHAR(50) NOT NULL," +
                        "description VARCHAR(50)," +
                        "code VARCHAR(9), " +
                        "hidden BOOL NOT NULL DEFAULT false," +
                        "has_similar_categories BOOL NOT NULL DEFAULT false," +
                        "can_have_products BOOL NOT NULL," +
                        "parent_category_id INTEGER NOT NULL DEFAULT 0, " +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP," +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') *1000), " +
                        "CONSTRAINT parent_category_fk FOREIGN KEY(parent_category_id) REFERENCES product_categories(category_id))").Execute();


                    /* Categories that are similar. Helper Table, not a Main Working Table. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS similar_product_categories( " +
                        "first_category_id INTEGER DEFAULT 0," +
                        "second_category_id INTEGER DEFAULT 0,  " +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP," +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') *1000), " +
                        "PRIMARY KEY(first_category_id, second_category_id)," +
                        "CONSTRAINT first_category_fk FOREIGN KEY(first_category_id) REFERENCES product_categories(category_id)," +
                        "CONSTRAINT second_category_fk FOREIGN KEY(second_category_id) REFERENCES product_categories(category_id))").Execute();


                    /* Store all the UOMs that the Products Need +
                     * We can have a group that all the users exist in, and this would allow offline sync for tables which are not multi-tenant
                     */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS uom ( " +
                        "uom_id INTEGER PRIMARY KEY AUTOINCREMENT," +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "name VARCHAR(16) NOT NULL," +
                        "uom VARCHAR(4) NOT NULL," +
                        "parent_uom_id INTEGER NOT NULL DEFAULT 0," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP," +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000))").Execute();

                    /* The Tables that Will Maintain the Master of the Products. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS product_master ( " +
                        "product_master_id UUID PRIMARY KEY, " +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "name VARCHAR(36) NOT NULL, " +
                        "code VARCHAR(36) NOT NULL, " +
                        "category_id INTEGER NOT NULL, " +
                        "serial_num VARCHAR(36) NOT NULL, " +
                        "barcode VARCHAR(36) UNIQUE, " +
                        "base_price DECIMAL(10,2) NOT NULL DEFAULT 0, " +
                        "uom_id INTEGER NOT NULL, " +
                        "pack_size INTEGER NOT NULL DEFAULT 1," +
                        "has_multiple_variations BOOL NOT NULL DEFAULT false," +
                        "has_additions BOOL NOT NULL DEFAULT false," +
                        "is_service BOOL NOT NULL DEFAULT false, " +
                        "status VARCHAR(16) NOT NULL CONSTRAINT valid_status CHECK(status IN('ACTIVE', 'SUSPENDED', 'DEACTIVATED', 'IN ACTIVE', 'PENDING'))," +
                        "inventory_method VARCHAR(24) NOT NULL CONSTRAINT valid_inventory_method CHECK(inventory_method IN('FIFO', 'WEIGHTED AVERAGE', 'SPECIFIC IDENTIFICATION', 'LIFO'))," +
                        "is_periodic_inventory BOOL NOT NULL DEFAULT false," +
                        "thumbnail_image_id UUID," +
                        "has_images BOOL NOT NULL DEFAULT false," +
                        "weight DECIMAL(10,2) NOT NULL DEFAULT 0," +
                        "has_warranty BOOL NOT NULL DEFAULT false," +
                        "can_return BOOL NOT NULL DEFAULT false," +
                        "can_replace BOOL NOT NULL DEFAULT false," +
                        "can_refund BOOL NOT NULL DEFAULT false," +
                        "remark VARCHAR(16)," +
                        "brand VARCHAR(24)," +
                        "other_details VARCHAR," +
                        "expiration_date INTEGER NOT NULL DEFAULT 0," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP," +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') *1000)," +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id)," +
                        "CONSTRAINT uom_fk FOREIGN KEY(uom_id) REFERENCES uom(uom_id)," +
                        "CONSTRAINT category_fk FOREIGN KEY(category_id) REFERENCES product_categories(category_id))").Execute();

                    /* Maintain the Images of the Products. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS product_master_images ( " +
                        "prod_master_image_id UUID PRIMARY KEY," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "image_url TEXT NOT NULL, " +
                        "thumbnail_image_url TEXT," +
                        "product_id UUID NOT NULL," +
                        "title VARCHAR(50)," +
                        "description VARCHAR(50)," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP," +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') *1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id), " +
                        "CONSTRAINT product_fk FOREIGN KEY(product_id) REFERENCES product_master(product_master_id))").Execute();

                    /* Lets Handle all the Variations Available for the Products to Choose from. 
	                    'SIZE', 'COLOR', 'RAM', 'STORAGE', 'PROCESSING', 'HEIGHT', 'WIDTH' -> Different Types of Variations, 
	                    A Variation can be a Combination of the Above.

                     * The Tables that Will Maintain the Variations of the Product. 
                     * The Variation Price is Not an additive, it replaces the Base Price. 
                     * The Variation is Added as a JSON Object, where we can maintain the type & any additional params we have to maintain.
                       Example for the Variations JSON :
                       {
		                    'SIZE' : {
					                    'name' : 'LARGE',
					                    'attribute' : 'L'
				                     },
		                    'COLOR' : {
					                    'name' : 'TEAL',
					                    'attribute' : '#009688'
				                     }
                       }
 
                     */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS product_variations ( " +
                        "product_variation_id UUID PRIMARY KEY, " +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "product_id UUID NOT NULL, " +
                        "variations TEXT NOT NULL DEFAULT '{}', " +
                        "name VARCHAR(36) NOT NULL, " +
                        "code VARCHAR(36) NOT NULL," +
                        "barcode VARCHAR(36) UNIQUE," +
                        "category_id INTEGER NOT NULL, " +
                        "serial_num VARCHAR(36) NOT NULL, " +
                        "price_override DECIMAL(10, 2) NOT NULL DEFAULT 0, " +
                        "weight_override DECIMAL(10,2) NOT NULL DEFAULT 0," +
                        "has_images BOOL NOT NULL DEFAULT false," +
                        "thumbnail_image_id UUID," +
                        "has_warranty BOOL NOT NULL DEFAULT false," +
                        "can_return BOOL NOT NULL DEFAULT false," +
                        "can_replace BOOL NOT NULL DEFAULT false," +
                        "can_refund BOOL NOT NULL DEFAULT false," +
                        "status VARCHAR(16) NOT NULL CONSTRAINT valid_status CHECK(status IN('ACTIVE', 'SUSPENDED', 'DEACTIVATED', 'IN ACTIVE', 'PENDING')), " +
                        "remark_override VARCHAR(16)," +
                        "other_details_override TEXT," +
                        "expiration_date_override INTEGER NOT NULL DEFAULT 0," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id), " +
                        "CONSTRAINT product_fk FOREIGN KEY(product_id) REFERENCES product_master(product_master_id))").Execute();


                    /* Maintain the Images of the Product Variations. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS product_variation_images( " +
                        "prod_variation_image_id UUID PRIMARY KEY," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "image_url TEXT NOT NULL," +
                        "thumbnail_image_url TEXT," +
                        "product_variation_id UUID NOT NULL," +
                        "title VARCHAR(50)," +
                        "description VARCHAR(50)," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id)," +
                        "CONSTRAINT product_variation_fk FOREIGN KEY(product_variation_id) REFERENCES product_variations(product_variation_id))").Execute();


                    /* Lets Handle all the Additions Available for the Products to Choose from. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS product_additions(" +
                        "product_addition_id UUID PRIMARY KEY, " +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "product_id UUID NOT NULL, " +
                        "name VARCHAR(32) NOT NULL," +
                        "attribute VARCHAR(32)," +
                        "barcode TEXT UNIQUE," +
                        "price_addition DECIMAL(10,2) NOT NULL DEFAULT 0," +
                        "weight_addition DECIMAL(10,2) NOT NULL DEFAULT 0," +
                        "serial_num VARCHAR(36) NOT NULL," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id), " +
                        "CONSTRAINT product_fk FOREIGN KEY(product_id) REFERENCES product_master(product_master_id))").Execute();


                    /* Attributes of the Products & Their Variations. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS product_attributes(" +
                        "attribute_id UUID PRIMARY KEY," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "product_id UUID NOT NULL," +
                        "is_variation BOOL NOT NULL DEFAULT false," +
                        "name VARCHAR(32) NOT NULL," +
                        "attribute VARCHAR(32) NOT NULL," +
                        "secondary_info VARCHAR(32)," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id))").Execute();


                    /* Brand of the Products. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS product_brands( " +
                        "brand VARCHAR(24) PRIMARY KEY," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "manufacturer_name VARCHAR(50) NOT NULL," +
                        "manufacturer_description VARCHAR(100), " +
                        "code VARCHAR(9) NOT NULL," +
                        "tagline TEXT," +
                        "icon_img TEXT," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id))").Execute();


                    /* Product Warranty Table. Should also Hold Extended Warranties. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS product_warranties(" +
                        "warranty_id UUID PRIMARY KEY," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "product_id UUID NOT NULL," +
                        "is_variation BOOL NOT NULL DEFAULT false," +
                        "warranty_period INTEGER NOT NULL DEFAULT 0," +
                        "warranty_period_type VARCHAR(12) NOT NULL DEFAULT 'MONTH' CHECK(warranty_period_type IN('HOUR', 'DAY', 'MONTH', 'YEAR'))," +
                        "is_extended_warranty BOOL NOT NULL DEFAULT false," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id))").Execute();


                    /* Product Kits, Will Help Maintain a Kit of Products. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS product_kit(" +
                        "product_kit_id UUID PRIMARY KEY," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "name VARCHAR(36) NOT NULL," +
                        "code VARCHAR(36) NOT NULL," +
                        "category_id INT NOT NULL," +
                        "serial_num VARCHAR(36) NOT NULL," +
                        "kit_image_url TEXT," +
                        "kit_thumbnail_url TEXT," +
                        "base_price DECIMAL(10, 2) NOT NULL DEFAULT 0," +
                        "base_weight DECIMAL(10,2) NOT NULL DEFAULT 0," +
                        "is_service BOOL NOT NULL DEFAULT false," +
                        "status VARCHAR(16) NOT NULL CHECK(status IN('ACTIVE', 'SUSPENDED', 'DEACTIVATED', 'IN ACTIVE', 'PENDING'))," +
                        "has_warranty BOOL NOT NULL DEFAULT false," +
                        "remark VARCHAR(16)," +
                        "other_details TEXT," +
                        "expiration_date INT NOT NULL DEFAULT 0," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id))").Execute();


                    /* Product Kit's Products. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS product_kit_products(" +
                        "product_kit_id UUID NOT NULL," +
                        "product_id UUID NOT NULL," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "quantity DECIMAL(10,2) NOT NULL DEFAULT 0," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "PRIMARY KEY(product_kit_id, product_id)," +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id))").Execute();


                    /* The Products that are Wishlisted by the Customers or Buyers. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS product_wishlists ( " +
                        "product_master_id UUID NOT NULL," +
                        "customer_id UUID NOT NULL," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "is_wishlist BOOL NOT NULL DEFAULT true, " +
                        "customer_type VARCHAR(16) NOT NULL CONSTRAINT valid_customer_type CHECK(customer_type IN('BUYER', 'CONSUMER')), " +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "PRIMARY KEY(product_master_id, customer_id)," +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id)," +
                        "CONSTRAINT product_fk FOREIGN KEY(product_master_id) REFERENCES product_master(product_master_id))").Execute();


                    /* The Products that the Customer has Recently Viewed. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS recently_viewed_products (" +
                        "product_master_id UUID NOT NULL," +
                        "customer_id UUID NOT NULL," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "number_of_times_viewed INTEGER NOT NULL DEFAULT 1," +
                        "customer_type VARCHAR(16) NOT NULL CONSTRAINT valid_customer_type CHECK(customer_type IN('BUYER', 'CONSUMER')), " +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "PRIMARY KEY(product_master_id, customer_id)," +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id)," +
                        "CONSTRAINT product_fk FOREIGN KEY(product_master_id) REFERENCES product_master(product_master_id))").Execute();

                    /* The Product Groups. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS product_groups( " +
                        "group_id INTEGER PRIMARY KEY, " +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "name VARCHAR(50) NOT NULL," +
                        "description VARCHAR(100) NOT NULL, " +
                        "group_image_url TEXT, " +
                        "thumbnail_image_url TEXT," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id))").Execute();

                    /* The Products in the Group of the Products */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS prod_group_products ( " +
                        "product_master_id UUID NOT NULL, " +
                        "group_id INTEGER NOT NULL, " +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "PRIMARY KEY (product_master_id, group_id), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY (tenant_id_) REFERENCES companies(id), " +
                        "CONSTRAINT product_master_fk FOREIGN KEY (product_master_id) REFERENCES product_master(id), " +
                        "CONSTRAINT product_group_fk FOREIGN KEY (group_id) REFERENCES product_groups(id))").Execute();




                    /* Store Details like Outlet, Warehouse Stores, Popup Stores. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS stores(" +
                        "store_id INTEGER PRIMARY KEY," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "name VARCHAR(50) NOT NULL," +
                        "secondary_info VARCHAR(50)," +
                        "email_id VARCHAR(50) CONSTRAINT valid_email_id CHECK ((email_id LIKE '%_@__%.__%' AND email_id = LOWER(email_id) AND LENGTH(email_id) <= 50) OR LENGTH(email_id) = 0 OR NULL), " +
                        "phone_number BIGINT NOT NULL CONSTRAINT valid_phone_number CHECK(LENGTH(phone_number) BETWEEN 8 AND 12), " +
                        "store_website TEXT," +
                        "type VARCHAR(16) NOT NULL CONSTRAINT valid_type CHECK(type IN('MOM N POP', 'POPUP', 'MALL', 'DEPARTMENT', 'DISCOUNT', 'SUPERMARKET', 'WAREHOUSE OUTLET', 'FACTORY OUTLET', 'E TAILERS'))," +
                        "primary_warehouse_id UUID," +
                        "can_deliver BOOL NOT NULL DEFAULT false," +
                        "other_details TEXT," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id))").Execute();


                    /* Store Addresses. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS store_addresses(" +
                        "store_id INTEGER PRIMARY KEY," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "address_line1 VARCHAR(100) NOT NULL," +
                        "address_line2 VARCHAR(100), " +
                        "address_line3 VARCHAR(100), " +
                        "landmark VARCHAR(100), " +
                        "pincode VARCHAR(10) NOT NULL," +
                        "city VARCHAR(50) NOT NULL," +
                        "state_id INTEGER NOT NULL, " +
                        "state_name VARCHAR(50)," +
                        "lt_num DECIMAL(6, 6) NOT NULL DEFAULT 0.0," +
                        "ln_num DECIMAL(6, 6) NOT NULL DEFAULT 0.0," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id))").Execute();


                    /* Store's Weekly Hours. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS store_timings(" +
                        "store_id INT PRIMARY KEY," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "monday_opening_hours DECIMAL(2,2) NOT NULL DEFAULT 0 CHECK(monday_opening_hours BETWEEN 0.00 AND 23.59)," +
                        "monday_closing_hours DECIMAL(2,2) NOT NULL DEFAULT 23.59 CHECK(monday_closing_hours BETWEEN 0.00 AND 23.59)," +
                        "tuesday_opening_hours DECIMAL(2,2) NOT NULL DEFAULT 0 CHECK(tuesday_opening_hours BETWEEN 0.00 AND 23.59)," +
                        "tuesday_closing_hours DECIMAL(2,2) NOT NULL DEFAULT 23.59 CHECK(tuesday_closing_hours BETWEEN 0.00 AND 23.59)," +
                        "wednesday_opening_hours DECIMAL(2,2) NOT NULL DEFAULT 0 CHECK(wednesday_opening_hours BETWEEN 0.00 AND 23.59)," +
                        "wednesday_closing_hours DECIMAL(2,2) NOT NULL DEFAULT 23.59 CHECK(wednesday_closing_hours BETWEEN 0.00 AND 23.59)," +
                        "thursday_opening_hours DECIMAL(2,2) NOT NULL DEFAULT 0 CHECK(thursday_opening_hours BETWEEN 0.00 AND 23.59)," +
                        "thursday_closing_hours DECIMAL(2,2) NOT NULL DEFAULT 23.59 CHECK(thursday_closing_hours BETWEEN 0.00 AND 23.59)," +
                        "friday_opening_hours DECIMAL(2,2) NOT NULL DEFAULT 0 CHECK(friday_opening_hours BETWEEN 0.00 AND 23.59)," +
                        "friday_closing_hours DECIMAL(2,2) NOT NULL DEFAULT 23.59 CHECK(friday_closing_hours BETWEEN 0.00 AND 23.59)," +
                        "saturday_opening_hours DECIMAL(2,2) NOT NULL DEFAULT 0 CHECK(saturday_opening_hours BETWEEN 0.00 AND 23.59)," +
                        "saturday_closing_hours DECIMAL(2,2) NOT NULL DEFAULT 23.59 CHECK(saturday_closing_hours BETWEEN 0.00 AND 23.59)," +
                        "sunday_opening_hours DECIMAL(2,2) NOT NULL DEFAULT 0 CHECK(sunday_opening_hours BETWEEN 0.00 AND 23.59)," +
                        "sunday_closing_hours DECIMAL(2,2) NOT NULL DEFAULT 23.59 CHECK(sunday_closing_hours BETWEEN 0.00 AND 23.59)," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id))").Execute();


                    /* Manage the Employees working in the Store. (The Staff) */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS store_staff(" +
                        "store_id INTEGER NOT NULL," +
                        "employee_id UUID NOT NULL," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "reports_to_user_id UUID," +
                        "date_of_birth BIGINT NOT NULL DEFAULT 0," +
                        "status STRING(16) NOT NULL DEFAULT 'ACTIVE' CHECK(status IN('ACTIVE', 'SUSPENDED WITHOUT PAY', 'SUSPENDED WITH PAY', 'LET GO', 'RESIGNED', 'TRANSFERED'))," +
                        "gender STRING(12) NOT NULL DEFAULT 'NOT SET' CHECK(gender IN('NOT SET', 'MALE', 'FEMALE', 'TRANSGENDER'))," +
                        "responsibility STRING(50)," +
                        "other_details STRING," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "PRIMARY KEY(store_id, employee_id)," +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id))").Execute();


                    /* Inventory of the Stores. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS store_inventory_ledger(" +
                        "id UUID PRIMARY KEY," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "product_variation_id UUID NOT NULL," +
                        "store_id INTEGER NOT NULL, " +
                        "transaction_date BIGINT NOT NULL DEFAULT 0, " +
                        "transaction_price DECIMAL(10,2) NOT NULL DEFAULT 0," +
                        "max_retail_price DECIMAL(10,2) NOT NULL DEFAULT 0," +
                        "tentitive_selling_price DECIMAL(10,2) NOT NULL DEFAULT 0," +
                        "quantity_transfered INTEGER NOT NULL DEFAULT 0," +
                        "quantity_available INTEGER NOT NULL DEFAULT 0," +
                        "batch_id INTEGER NOT NULL DEFAULT 0," +
                        "serial_num VARCHAR(36) NOT NULL," +
                        "barcode VARCHAR(36) UNIQUE," +
                        "expiry_date BIGINT NOT NULL DEFAULT 0," +
                        "transaction_type VARCHAR(12) NOT NULL CHECK(transaction_type IN('PURCHASE', 'SALE'))," +
                        "suplier_id INTEGER NOT NULL DEFAULT 0," +
                        "suplier_type VARCHAR(12) NOT NULL CHECK(suplier_type IN('STORE', 'WAREHOUSE', 'FACTORY', 'SELLER', 'HEAD OFFICE'))," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id)," +
                        "CONSTRAINT product_variation_fk FOREIGN KEY(product_variation_id) REFERENCES product_variations(product_variation_id)," +
                        "CONSTRAINT store_fk FOREIGN KEY(store_id) REFERENCES stores(store_id))").Execute();



                    /* Warehouse Details to maintain Pure Warehouses. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS warehouses(" +
                        "warehouse_id INTEGER PRIMARY KEY," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "name VARCHAR(50) NOT NULL," +
                        "secondary_info VARCHAR(50)," +
                        "email_id VARCHAR(50) CONSTRAINT valid_email_id CHECK ((email_id LIKE '%_@__%.__%' AND email_id = LOWER(email_id) AND LENGTH(email_id) <= 50) OR LENGTH(email_id) = 0 OR NULL),   " +
                        "phone_number BIGINT NOT NULL CONSTRAINT valid_phone_number CHECK(LENGTH(phone_number) BETWEEN 8 AND 12), " +
                        "can_sell BOOL NOT NULL DEFAULT false," +
                        "can_deliver BOOL NOT NULL DEFAULT false," +
                        "other_details TEXT," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id))").Execute();


                    /* Warehouse Addresses. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS warehouse_addresses(" +
                        "warehouse_id INTEGER PRIMARY KEY," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "address_line1 VARCHAR(100) NOT NULL," +
                        "address_line2 VARCHAR(100), " +
                        "address_line3 VARCHAR(100), " +
                        "landmark VARCHAR(100), " +
                        "pincode VARCHAR(10) NOT NULL," +
                        "city VARCHAR(50) NOT NULL," +
                        "state_id INT NOT NULL, " +
                        "state_name VARCHAR(50)," +
                        "lt_num DECIMAL(6, 6) NOT NULL DEFAULT 0.0," +
                        "ln_num DECIMAL(6, 6) NOT NULL DEFAULT 0.0," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id))").Execute();


                    /* Manage the Employees working in the Warehouse. (The Staff) */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS warehouse_staff(" +
                        "warehouse_id INTEGER NOT NULL," +
                        "employee_id UUID NOT NULL," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "reports_to_user_id UUID," +
                        "date_of_birth BIGINT NOT NULL DEFAULT 0," +
                        "status VARCHAR(16) NOT NULL DEFAULT 'ACTIVE' CONSTRAINT valid_status CHECK(status IN('ACTIVE', 'SUSPENDED WITHOUT PAY', 'SUSPENDED WITH PAY', 'LET GO', 'RESIGNED', 'TRANSFERED'))," +
                        "gender VARCHAR(12) NOT NULL DEFAULT 'NOT SET' CONSTRAINT valid_gender CHECK(gender IN('NOT SET', 'MALE', 'FEMALE', 'TRANSGENDER'))," +
                        "responsibility VARCHAR(50)," +
                        "other_details TEXT," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "PRIMARY KEY(warehouse_id, employee_id)," +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id))").Execute();


                    /* Inventory of the Warehouses. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS warehouse_inventory_ledger(" +
                        "id UUID PRIMARY KEY," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "product_variation_id UUID NOT NULL," +
                        "warehouse_id INTEGER NOT NULL, " +
                        "transaction_date BIGINT NOT NULL DEFAULT 0, " +
                        "transaction_price DECIMAL(10,2) NOT NULL DEFAULT 0," +
                        "max_retail_price DECIMAL(10,2) NOT NULL DEFAULT 0," +
                        "tentitive_selling_price DECIMAL(10,2) NOT NULL DEFAULT 0," +
                        "quantity_transfered INTEGER NOT NULL DEFAULT 0," +
                        "quantity_available INTEGER NOT NULL DEFAULT 0," +
                        "batch_id INTEGER NOT NULL DEFAULT 0," +
                        "serial_num VARCHAR(36) NOT NULL," +
                        "barcode VARCHAR(36) UNIQUE," +
                        "expiry_date BIGINT NOT NULL DEFAULT 0," +
                        "transaction_type VARCHAR(12) NOT NULL CHECK(transaction_type IN('PURCHASE', 'SALE'))," +
                        "suplier_id INTEGER NOT NULL DEFAULT 0," +
                        "suplier_type VARCHAR(12) NOT NULL CHECK(suplier_type IN('STORE', 'WAREHOUSE', 'FACTORY', 'SELLER', 'HEAD OFFICE'))," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id)," +
                        "CONSTRAINT product_variation_fk FOREIGN KEY(product_variation_id) REFERENCES product_variations(product_variation_id)," +
                        "CONSTRAINT warehouse_fk FOREIGN KEY(warehouse_id) REFERENCES warehouses(warehouse_id))").Execute();



                    /* Transportation Related Tables will Come Here. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS transporters( " +
                        "transporter_id INTEGER PRIMARY KEY," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "transporter_name VARCHAR(50) NOT NULL," +
                        "transporter_alias VARCHAR(50) NOT NULL," +
                        "gst_in VARCHAR(15) CONSTRAINT valid_gst CHECK(LENGTH(gst_in) = 15 OR NULL), " + /* GST Validation should be carried out by Code. */
                        "pan_num VARCHAR(10) CONSTRAINT valid_pan_num CHECK(LENGTH(pan_num) = 10 OR NULL)," +
                        "email_id VARCHAR(50) CONSTRAINT valid_email_id CHECK(email_id LIKE '%_@__%.__%' AND email_id = LOWER(email_id) AND LENGTH(email_id) <= 50), " +
                        "phone_number BIGINT NOT NULL CONSTRAINT valid_phone_number CHECK(LENGTH(phone_number) BETWEEN 8 AND 12), " +
                        "city VARCHAR(50) NOT NULL," +
                        "state_id BIGINT NOT NULL," +
                        "other_details VARCHAR," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id))").Execute();


                    /* Vehical Numbers of the Transporters. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS transporter_vehicals(" +
                        "vehical_num VARCHAR(50) NOT NULL," +
                        "transporter_id INTEGER NOT NULL, " +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "vehical_type VARCHAR(16) NOT NULL DEFAULT 'LORRY' CHECK(vehical_type IN('LORRY', 'TRUCK', 'TEMPO', 'BIKE', 'CAR', 'AIRPLANE', 'SHIP', 'RAIL'))," +
                        "registration_num VARCHAR(64)," +
                        "chase_num VARCHAR(64)," +
                        "location_tracking_id VARCHAR(36)," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "PRIMARY KEY(vehical_num, transporter_id)," +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id))").Execute();


                    /* Inventory in the Transportation. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS transport_inventory_ledger(" +
                        "id UUID PRIMARY KEY," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "product_variation_id UUID NOT NULL," +
                        "transporter_id INTEGER NOT NULL, " +
                        "transaction_date BIGINT NOT NULL DEFAULT 0, " +
                        "transaction_price DECIMAL(10,2) NOT NULL DEFAULT 0," +
                        "max_retail_price DECIMAL(10,2) NOT NULL DEFAULT 0," +
                        "tentitive_selling_price DECIMAL(10,2) NOT NULL DEFAULT 0," +
                        "quantity_transfered INTEGER NOT NULL DEFAULT 0," +
                        "quantity_available INTEGER NOT NULL DEFAULT 0," +
                        "batch_id INTEGER NOT NULL DEFAULT 0," +
                        "serial_num VARCHAR(36) NOT NULL," +
                        "barcode VARCHAR(36) UNIQUE," +
                        "expiry_date BIGINT NOT NULL DEFAULT 0," +
                        "transaction_type VARCHAR(12) NOT NULL CHECK(transaction_type IN('PURCHASE', 'SALE'))," +
                        "suplier_id INTEGER NOT NULL DEFAULT 0," +
                        "suplier_type VARCHAR(12) NOT NULL CHECK(suplier_type IN('STORE', 'WAREHOUSE', 'FACTORY', 'SELLER', 'HEAD OFFICE'))," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s','now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id)," +
                        "CONSTRAINT product_variation_fk FOREIGN KEY(product_variation_id) REFERENCES product_variations(product_variation_id)," +
                        "CONSTRAINT transporter_fk FOREIGN KEY(transporter_id) REFERENCES transporters(transporter_id))").Execute();



                    /* Create the Invoice Table. We will Store all the Invoices. */
                    /* The Main Table of the Invoices */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS sales (" +
                        "sales_id UUID PRIMARY KEY," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0," +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "invoice_type VARCHAR(32) NOT NULL CONSTRAINT valid_invoice_type CHECK(invoice_type IN('FULL', 'SMALL', 'POS'))," +
                        "sale_locked BOOL NOT NULL DEFAULT false, " +
                        "sale_date BIGINT NOT NULL," +
                        "due_date BIGINT DEFAULT 0," +
                        "has_customer BOOL NOT NULL DEFAULT false," +
                        "has_billing_address BOOL NOT NULL DEFAULT false," +
                        "has_delivery_address BOOL NOT NULL DEFAULT false," +
                        "has_salesperson BOOL NOT NULL DEFAULT false," +
                        "has_transporter BOOL NOT NULL DEFAULT false," +
                        "status VARCHAR(40) NOT NULL DEFAULT 'SAVED' CONSTRAINT valid_status CHECK(status IN('SAVED', 'CHECKED OUT', 'VERIFICATION REQUIRED', 'PROCESSING', 'PARTIALLY SHIPPED', 'SHIPPED', 'ENROUTE', 'DELIVERED', 'CANCELLED', 'COMPLETED', 'DECLINED', 'RETURNED', 'PARTIALLY RETURNED', 'DISPUTED')), " +
                        "payment_status VARCHAR(40) NOT NULL DEFAULT 'UNPAID' CONSTRAINT valid_payment_status CHECK(payment_status IN('UNPAID', 'PARTIALLY PAID', 'PAID', 'REFUNDED', 'PARTIALLY REFUNDED'))," +
                        "previous_status TEXT," +
                        "previous_payment_status TEXT," +
                        "sales_channel VARCHAR(40) NOT NULL DEFAULT 'IN STORE' CONSTRAINT valid_sales_channel CHECK(sales_channel IN('IN STORE', 'ONLINE', 'BUYZAPP', 'WEBSITE', 'AMAZON', 'FLIPKART', 'SNAPDEAL', 'PAYTM'))," +
                        "payment_mode VARCHAR(100)," +
                        "delivery_terms VARCHAR(100)," +
                        "delivery_note VARCHAR(100)," +
                        "place_of_supply VARCHAR(255)," +
                        "remarks VARCHAR(100)," +
                        "country_id_of_supply INTEGER NOT NULL," +
                        "version_number INTEGER NOT NULL DEFAULT 1," +
                        "other_details TEXT," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s', 'now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id))").Execute();


                    /* Lets have the Address of the Invoices. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS sale_addresses (" +
                        "sales_id UUID NOT NULL, " +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0," +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "address_id_related_to UUID NOT NULL," +
                        "sale_locked BOOL NOT NULL DEFAULT false," +
                        "address_type VARCHAR(32) NOT NULL DEFAULT 'NOT DEFINED' CONSTRAINT valid_address_type CHECK(address_type IN('NOT DEFINED', 'BILLING', 'DELIVERY'))," +
                        "gst_in VARCHAR(15) CONSTRAINT valid_gst CHECK(LENGTH(gst_in) = 15 OR NULL)," +     /* GST Validation should be carried out by Code. */
                        "address_name VARCHAR(50)," +
                        "addressed_to_name VARCHAR(50), " +
                        "line1 VARCHAR(100) NOT NULL," +
                        "line2 VARCHAR(100), " +
                        "line3 VARCHAR(100), " +
                        "landmark VARCHAR(100), " +
                        "pincode VARCHAR(10) NOT NULL," +
                        "city VARCHAR(50) NOT NULL," +
                        "state_id INTEGER NOT NULL," +
                        "state_name VARCHAR(50), " +
                        "email_id VARCHAR(50) CONSTRAINT valid_email_id CHECK ((email_id LIKE '%_@__%.__%' AND email_id = LOWER(email_id) AND LENGTH(email_id) <= 50) OR LENGTH(email_id) = 0 OR NULL),  " +
                        "phone_number BIGINT CONSTRAINT valid_phone_number CHECK ((LENGTH(phone_number) BETWEEN 8 AND 12) OR phone_number = 0), " +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s', 'now') * 1000), " +
                        "PRIMARY KEY(sales_id, address_type), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id), " +
                        "CONSTRAINT sale_fk FOREIGN KEY(sales_id) REFERENCES sales(sales_id))").Execute();


                    /* The Sales Person Related to the Sale. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS sale_saleperson (" +
                        "sales_id UUID NOT NULL," +
                        "user_id UUID NOT NULL," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0," +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "status VARCHAR(32) NOT NULL DEFAULT 'ACTIVE' CONSTRAINT valid_status CHECK(status IN('ACTIVE', 'LEAD', 'SILENT', 'INACTIVE', 'REMOVED'))," +
                        "remarks VARCHAR(100), " +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s', 'now') * 1000), " +
                        "PRIMARY KEY(sales_id, user_id)," +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id)," +
                        "CONSTRAINT sale_fk FOREIGN KEY(sales_id) REFERENCES sales(sales_id)," +
                        "CONSTRAINT user_fk FOREIGN KEY(user_id) REFERENCES users(user_id))").Execute();


                    /* The Transporter Details of the Sale */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS sale_transporter (" +
                        "sales_id UUID NOT NULL," +
                        "transporter_id INTEGER NOT NULL DEFAULT 0," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0," +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "vehical_num VARCHAR(50) NOT NULL," +
                        "transporter VARCHAR(50) NOT NULL," +
                        "mode_of_transport VARCHAR(20) CONSTRAINT valid_mode_of_transport CHECK(mode_of_transport IN('ROAD', 'RAIL', 'AIR', 'SHIP'))," +
                        "has_eway_bill BOOL NOT NULL DEFAULT false," +
                        "remarks VARCHAR(100)," +
                        "other_details TEXT," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s', 'now') * 1000), " +
                        "PRIMARY KEY(sales_id, transporter_id), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id), " +
                        "CONSTRAINT sale_fk FOREIGN KEY(sales_id) REFERENCES sales(sales_id))").Execute();


                    /* The Customer of the Sale will be Stored. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS sale_customer ( " +
                        "sales_id UUID PRIMARY KEY, " +
                        "customer_id UUID NOT NULL, " +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0," +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "invoice_type VARCHAR(32) NOT NULL CONSTRAINT valid_invoice_type CHECK(invoice_type IN('FULL', 'SMALL', 'POS')), " +
                        "sale_locked BOOL NOT NULL DEFAULT false," +
                        "customer_name VARCHAR(50) NOT NULL, " +
                        "customer_alias VARCHAR(50) NOT NULL," +
                        "gst_in VARCHAR(15) CONSTRAINT valid_gst CHECK(LENGTH(gst_in) = 15 OR NULL)," +    /* GST Validation should be carried out by Code. */
                        "pan_num VARCHAR(10) CONSTRAINT valid_pan_num CHECK(LENGTH(pan_num) = 10 OR NULL), " +
                        "email_id VARCHAR(50) CONSTRAINT valid_email_id CHECK ((email_id LIKE '%_@__%.__%' AND email_id = LOWER(email_id) AND LENGTH(email_id) <= 50) OR LENGTH(email_id) = 0 OR NULL),  " +
                        "phone_number BIGINT NOT NULL CONSTRAINT valid_phone_number CHECK(LENGTH(phone_number) BETWEEN 8 AND 12), " +
                        "city VARCHAR(50), " +
                        "state_id INTEGER, " +
                        "other_details TEXT," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s', 'now') * 1000), " +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id))").Execute();


                    /* Warranty of the Products, Purchased. */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS sale_warranties( " +
                        "sales_id UUID NOT NULL," +
                        "warranty_id UUID NOT NULL," +
                        "product_order_id UUID NOT NULL," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0," +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "begin_time BIGINT NOT NULL DEFAULT(strftime('%s', 'now') * 1000)," +
                        "quantity INTEGER NOT NULL DEFAULT 1," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s', 'now') * 1000), " +
                        "PRIMARY KEY(sales_id, product_order_id, warranty_id)," +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id)," +
                        "CONSTRAINT sale_fk FOREIGN KEY(sales_id) REFERENCES sales(sales_id))").Execute();


                    /* Maintain the Products of a Sale */
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS sale_products(" +
                        "sales_id UUID NOT NULL," +
                        "product_ledger_id UUID NOT NULL," +
                        "tenant_id_ INTEGER NOT NULL DEFAULT 0," +
                        "rls_id_ INTEGER NOT NULL DEFAULT 0, " +
                        "rls_type_ TINYINT NOT NULL DEFAULT 0, " +
                        "sync_id_ UUID NOT NULL UNIQUE, " +
                        "sale_location_type VARCHAR(16) NOT NULL CHECK(sale_location_type IN('STORE', 'WAREHOUSE', 'TRANSPORT', 'OTHER')), " +
                        "quantity INTEGER NOT NULL DEFAULT 0, " +
                        "price_per_unit DECIMAL(10,2) NOT NULL DEFAULT 0," +
                        "discount DECIMAL(2,2) NOT NULL DEFAULT 0, " +
                        "promo_applied BOOL NOT NULL DEFAULT false, " +
                        "other_details TEXT," +
                        "time_stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP, " +
                        "update_time_ INTEGER DEFAULT(strftime('%s', 'now') * 1000), " +
                        "PRIMARY KEY(sales_id, product_ledger_id)," +
                        "CONSTRAINT tenant_fk FOREIGN KEY(tenant_id_) REFERENCES companies(id))").Execute();




                    // Create the Table to Maintain all the Notifications of the System.
                    allTablesCreated = new Create().Table("CREATE TABLE IF NOT EXISTS user_notifications ("
                        + "id VARCHAR(36) PRIMARY KEY, "
                        + "tenant_id_ INTEGER NOT NULL, "
                        + "rls_id_ INTEGER NOT NULL DEFAULT 0, "
                        + "rls_type_ TINYINT NOT NULL DEFAULT 0, "
                        + "sync_id_ VARCHAR(36) NOT NULL CONSTRAINT valid_sync_id_ CHECK(LENGTH(sync_id_) = 36), "
                        + "icon VARCHAR(50) NOT NULL, "
                        + "title VARCHAR(50) NOT NULL, "
                        + "content VARCHAR(255) NOT NULL, "
                        + "color VARCHAR(50) NOT NULL DEFAULT 'accent', "
                        + "button_icon VARCHAR(50) NOT NULL DEFAULT 'tick_accent.png', "
                        + "button_hover_icon VARCHAR(50) NOT NULL DEFAULT 'tick.png', "
                        + "image VARCHAR(255), "
                        + "viewed BOOL NOT NULL DEFAULT false, "
                        + "time_stamp DATETIME DEFAULT CURRENT_TIMESTAMP, "
                        + "update_time_ INTEGER DEFAULT (strftime('%s','now') * 1000))").Execute();


                    /* Lets Run all the Index Creations on a Separate Thread. */
                    new Thread(() =>
                    {
                        try
                        {
                            // Now lets Create all the Indexes.
                            new Create().Index("CREATE UNIQUE INDEX consumer_email_id_idx_ ON consumers(tenant_id_, email_id)").Execute();
                            new Create().Index("CREATE UNIQUE INDEX consumer_phone_idx_ ON consumers(tenant_id_, phone_number)").Execute();
                            new Create().Index("CREATE UNIQUE INDEX buyer_email_id_idx_ ON buyers(tenant_id_, email_id)").Execute();
                            new Create().Index("CREATE UNIQUE INDEX buyer_phone_idx_ ON buyers(tenant_id_, phone_number)").Execute();
                            new Create().Index("CREATE INDEX office_addresses_lt_idx ON office_addresses(lt)").Execute();
                            new Create().Index("CREATE INDEX office_addresses_ln_idx ON office_addresses(ln)").Execute();
                            new Create().Index("CREATE UNIQUE INDEX product_master_name_idx_ ON product_master(tenant_id_, name)").Execute();
                            new Create().Index("CREATE UNIQUE INDEX product_master_serial_num_idx_ ON product_master(tenant_id_, serial_num)").Execute();
                            new Create().Index("CREATE UNIQUE INDEX product_master_code_idx_ ON product_master(tenant_id_, code)").Execute();
                        }
                        catch (Exception e)
                        {
                            // There was an Error.
                        }
                    }).Start();

                }
                catch (Exception e)
                {
                    // There was an Error.
                    allTablesCreated = false;
                }

                // Now we will insert into the table meta data.
                try
                {
                    // Lets enter all the table meta into the table.
                    CloudDB cdb = new CloudDB(C.SYSTEM_USER_DB_PASSCODE);

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("countries"))
                            .PutColumn(new ColumnData("name").Put("countries"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(false))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("states"))
                            .PutColumn(new ColumnData("name").Put("states"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(false))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("companies"))
                            .PutColumn(new ColumnData("name").Put("companies"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(false))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("company_billing_address"))
                            .PutColumn(new ColumnData("name").Put("company_billing_address"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(false))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("office_addresses"))
                            .PutColumn(new ColumnData("name").Put("office_addresses"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("users"))
                            .PutColumn(new ColumnData("name").Put("users"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("features"))
                            .PutColumn(new ColumnData("name").Put("features"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(false))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("features_table_map"))
                            .PutColumn(new ColumnData("name").Put("features_table_map"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(false))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("feature_user_rls_map"))
                            .PutColumn(new ColumnData("name").Put("feature_user_rls_map"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("user_notifications"))
                            .PutColumn(new ColumnData("name").Put("user_notifications"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("consumers"))
                            .PutColumn(new ColumnData("name").Put("consumers"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("consumer_billing"))
                            .PutColumn(new ColumnData("name").Put("consumer_billing"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("consumer_addresses"))
                            .PutColumn(new ColumnData("name").Put("consumer_addresses"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("buyers"))
                            .PutColumn(new ColumnData("name").Put("buyers"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("buyer_addresses"))
                            .PutColumn(new ColumnData("name").Put("buyer_addresses"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("sales"))
                            .PutColumn(new ColumnData("name").Put("sales"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("sale_customer"))
                            .PutColumn(new ColumnData("name").Put("sale_customer"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("sale_addresses"))
                            .PutColumn(new ColumnData("name").Put("sale_addresses"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("sale_saleperson"))
                            .PutColumn(new ColumnData("name").Put("sale_saleperson"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("sale_transporter"))
                            .PutColumn(new ColumnData("name").Put("sale_transporter"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("product_categories"))
                            .PutColumn(new ColumnData("name").Put("product_categories"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(false))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("uom"))
                            .PutColumn(new ColumnData("name").Put("uom"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(false))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("product_master"))
                            .PutColumn(new ColumnData("name").Put("product_master"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("product_master_images"))
                            .PutColumn(new ColumnData("name").Put("product_master_images"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("product_variations"))
                            .PutColumn(new ColumnData("name").Put("product_variations"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("product_variation_images"))
                            .PutColumn(new ColumnData("name").Put("product_variation_images"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("product_additions"))
                            .PutColumn(new ColumnData("name").Put("product_additions"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("product_attributes"))
                            .PutColumn(new ColumnData("name").Put("product_attributes"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("product_brands"))
                            .PutColumn(new ColumnData("name").Put("product_brands"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("product_warranties"))
                            .PutColumn(new ColumnData("name").Put("product_warranties"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("product_kit"))
                            .PutColumn(new ColumnData("name").Put("product_kit"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("product_kit_products"))
                            .PutColumn(new ColumnData("name").Put("product_kit_products"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("product_wishlists"))
                            .PutColumn(new ColumnData("name").Put("product_wishlists"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("recently_viewed_products"))
                            .PutColumn(new ColumnData("name").Put("recently_viewed_products"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("stores"))
                            .PutColumn(new ColumnData("name").Put("stores"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("store_addresses"))
                            .PutColumn(new ColumnData("name").Put("store_addresses"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("store_timings"))
                            .PutColumn(new ColumnData("name").Put("store_timings"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("store_staff"))
                            .PutColumn(new ColumnData("name").Put("store_staff"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("store_inventory_ledger"))
                            .PutColumn(new ColumnData("name").Put("store_inventory_ledger"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("warehouses"))
                            .PutColumn(new ColumnData("name").Put("warehouses"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("warehouse_addresses"))
                            .PutColumn(new ColumnData("name").Put("warehouse_addresses"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("warehouse_staff"))
                            .PutColumn(new ColumnData("name").Put("warehouse_staff"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("warehouse_inventory_ledger"))
                            .PutColumn(new ColumnData("name").Put("warehouse_inventory_ledger"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("transporters"))
                            .PutColumn(new ColumnData("name").Put("transporters"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("transporter_vehicals"))
                            .PutColumn(new ColumnData("name").Put("transporter_vehicals"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("transport_inventory_ledger"))
                            .PutColumn(new ColumnData("name").Put("transport_inventory_ledger"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("sale_warranties"))
                            .PutColumn(new ColumnData("name").Put("sale_warranties"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();

                    new Insert(cdb)
                            .IntoSystem("tables_")
                            .PutColumn(new ColumnData("id").Put("sale_products"))
                            .PutColumn(new ColumnData("name").Put("sale_products"))
                            .PutColumn(new ColumnData("syncable").Put(true))
                            .PutColumn(new ColumnData("offline_only").Put(false))
                            .PutColumn(new ColumnData("multi_tenant").Put(true))
                            .Execute();


                }
                catch (Exception e)
                {
                    // There was an Error.
                }
                return allTablesCreated;
            }
            catch (Exception e)
            {
                // There was an Error.
                return false;
            }
        }

    }
}
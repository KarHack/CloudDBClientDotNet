using _36E_Business___ERP.security;
using System;

namespace _36E_Business___ERP.cloudDB
{
    /*
     * 
     * Here we will store the User Data that has logged into the system.
     * 
     */
    class User
    {

        // Variables.
        private string status;
        private string databaseID = null;
        private long tenantID = 0;
        private string userUID;
        private string token;   // This is just the User Token. (160 Characters)
        private string timeStamp;
        private Device device;
        private bool isActive;
        internal bool IsActive { get { return isActive; } }

        // Constructor.
        private User()
        {
            // We do not require this constructor.
        }

        // Constructor to Set the required details.
        internal User(string userUID, string token, Device device, string databaseID, long tenantID)
        {
            // Set the required data.
            this.userUID = userUID;
            this.token = token;
            this.databaseID = databaseID;
            this.tenantID = tenantID;
            // Set the User ID in this Device.
            device.SetUserID(userUID);
            this.device = device;
        }

        // Get the User ID.
        public string GetUserID()
        {
            return userUID;
        }

        // Get the User Token.
        public string GetToken()
        {
            return token;
        }    

        // Attach the tenant id.
        void SetTenantID(long tenantID)
        {
            this.tenantID = tenantID;
        }

        // Get the tenant id.
        public long GetTenantID()
        {
            return tenantID;
        }

        // Get the Database id.
        public string GetDatabaseID()
        {
            return databaseID;
        }

        // Get the Status of the User.
        public string GetStatus()
        {
            return status;
        }

        // Get Device Info.
        public Device GetDevice()
        {
            try
            {
                // Lets Search for the Device in the Devices List.
                return device;
            }
            catch (Exception e)
            {
                // There was an Error.
                return null;
            }
        }

        // Add a device to the system.
        bool SetDevice(Device device)
        {
            try
            {
                // Here ew will add the Device to the Cache.
                // Set the User ID in this Device.
                device.SetUserID(userUID);
                this.device = device;
                return true;
            }
            catch (Exception e)
            {
                // There was an Error.
                return false;
            }
        }

        // Set the Timestamp.
        void SetTimeStamp(string timestamp)
        {
            this.timeStamp = timestamp;
        }

        // Get the Timestamp of this user.
        public string GetTimeStamp()
        {
            return timeStamp;
        }

        // Get and set the User Active Flag.
        public void SetActive(bool isActive)
        {
            this.isActive = isActive;
        }
        
    }

    /*
     * 
     * Here we will store the Device data of the user.
     *
     */
    class Device
    {

        // Variables.
        //private string status;
        private string deviceUID;
        private string token;   // This is the full token, that includes the User Token and the Device Token.	(256 Characters)
        private string deviceToken; // This is just the device token.   (96 Characters)
        private string pushToken;
        private Type type;
        private string deviceID;
        private string timeStamp;
        private string userID;

        // Static variables.
        // Static Types.
        public enum Type
        {
            WINDOWS, WEB, ANDROID, IOS
        }

        // Constructors.
        // Default Constructor.
        private Device()
        {
            // Nothing to do here.
        }

        // Constructor that we require.
        public Device(string deviceUID, string token)
        {
            try
            {
                // Set the Required Items.
                this.deviceUID = deviceUID;
                this.token = token;
                this.deviceToken = Auth.ExtractToken(token)[1];
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        public Device(string deviceUID, string token, string deviceToken)
        {
            try
            {
                // Set the Required Items.
                this.deviceUID = deviceUID;
                this.token = token;
                this.deviceToken = deviceToken;
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // Required Methods.
        internal void SetPushToken(string pushToken)
        {
            this.pushToken = pushToken;
        }

        // Here we get the required data.
        public string GetDeviceUID()
        {
            return deviceUID;
        }

        public string GetPushToken()
        {
            return pushToken;
        }

        public string GetToken()
        {
            return token;
        }

        // This method automatically calculates the device token.
        void SetToken(string token)
        {
            try
            {
                // Here we set the token and then calculate the device token as well.
                // Set the full token.
                this.token = token;
                this.deviceToken = Auth.ExtractToken(token)[1];
            }
            catch (Exception e)
            {
                // There was an Error.
            }
        }

        // Get the device token.
        public string GetDeviceToken()
        {
            return deviceToken;
        }

        // Set the Device type.
        void SetType(Type type)
        {
            this.type = type;
        }

        // Get the Device type.
        public Type GetType()
        {
            return type;
        }

        // Get and Set the Timestamp of this device.
        void SetTimeStamp(string timeStamp)
        {
            this.timeStamp = timeStamp;
        }

        // Set and Get the Device ID.
        void SetDeviceID(string deviceID)
        {
            this.deviceID = deviceID;
        }

        internal string GetDeviceID()
        {
            return this.deviceID;
        }

        internal string GetTimeStamp()
        {
            return timeStamp;
        }

        internal void SetUserID(string userID)
        {
            this.userID = userID;
        }

        internal string GetUserID()
        {
            return userID;
        }

    }

}
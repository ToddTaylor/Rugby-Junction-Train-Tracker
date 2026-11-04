namespace Web.Server.Hubs
{
    public static class NotificationMethods
    {
        public const string BeaconUpdate = "BeaconUpdate";
        public const string MapPinUpdate = "MapPinUpdate";
        public const string MapPinRemoved = "MapPinRemoved";
        public const string TrackedPinRemoved = "TrackedPinRemoved";
        public const string PassengerPinUpdate = "PassengerPinUpdate";
        public const string PassengerPinRemoved = "PassengerPinRemoved";
        public const string ServerStatusChanged = "ServerStatusChanged";
        public const string ServerCommandUpdated = "ServerCommandUpdated";
    }
}

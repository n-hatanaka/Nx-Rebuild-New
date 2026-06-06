namespace NxRebuild.Client.Pages.NxPrograms.DB {
    public class DataObj {
        private string _dataname;

        private string _tblName;
        private string _s_tblName;

        private string _w_tblName;
        private string _ws_tblName;

        private string _datatype;

        private Guid _lockerID;
        private DateTime _lockedat;


        public string DataName { get => _dataname; set { _dataname = value; }}
        public Guid LockerID { get => _lockerID; set { _lockerID = value; }}
        public DateTime LockedAt { get=>_lockedat; set { _lockedat = value; }}

    }
}

using NxRebuild.shared;

namespace NxRebuild.Client.Pages.NxPrograms.DB {
    // RyLibEntity のサーバー同期ラッパー（SyncZmstEntity と同じ構成）
    public class SyncRyLibEntity : SyncBaseDataObj<Guid>, IRyLibEntity {

        public override string ApiRoute => "RyLib";   // サーバー側コントローラのルートに合わせること

        // --- RyLibEntity へのアクセスを簡略化するためのプロパティ ---
        private RyLibEntity RyLib => (RyLibEntity)_dataObj;

        public decimal Ninzu {
            get => RyLib.Ninzu;
            set => RyLib.Ninzu = value;
        }

        public string? RyNote {
            get => RyLib.RyNote;
            set => RyLib.RyNote = value;
        }
    }
}
namespace NxRebuild.Client.Pages.NxPrograms.MDI
{
    public class WindowInfo
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = "";
        public Type ComponentType { get; set; } = default!;
        public Dictionary<string, object>? Parameters { get; set; }
        public double X { get; set; } = 100;
        public double Y { get; set; } = 100;

        public double Width { get; set; } = 600;
        public double Height { get; set; } = 400;

        public bool IsMaximized { get; set; }
        public bool IsMinimized { get; set; }


        // 最大化前の位置とサイズ
        public double PrevX { get; set; }
        public double PrevY { get; set; }
        public double PrevWidth { get; set; }
        public double PrevHeight { get; set; }

        public string Xpx => $"{X}px";
        public string Ypx => $"{Y}px";
        public string Wpx => $"{Width}px";
        public string Hpx => $"{Height}px";
        public int Z { get; set; } = 1;
    }
}

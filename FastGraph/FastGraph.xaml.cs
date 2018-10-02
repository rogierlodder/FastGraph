using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RLControls
{
    /// <summary>
    /// Interaction logic for FastGraph.xaml
    /// </summary>
    public partial class FastGraph : UserControl
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public FastGraph()
        {
            InitializeComponent();

            //zooming rectangle settings
            ZoomRect.Fill = Brushes.Blue;
            ZoomRect.Stroke = Brushes.Black;
            ZoomRect.Opacity = 0.2;
            ZoomMargin = 25;
            MaxNrPointsWithCaret = 400;

            //the zoom factor when the mousewheel is used
            MouseWheelZoomFactor = 0.2;

            //cursor settings
            Xcur.Stroke = Ycur.Stroke = Brushes.LightSteelBlue;
            Xcur.StrokeThickness = Ycur.StrokeThickness = 1;

            //default viewport
            World.Xmin = 0;
            World.Xmax = 1;
            World.Ymin = 0;
            World.Ymax = 1;

            //assign methods to the converter delegates
            Convert = NoConvert;
            ConvertBack = NoConvert;
            ZoomSet = false;
        }

        #region Private fields
        private bool ZoomStarted = false;
        private bool DragStarted = false;
        private bool ControlStarted = false;

        private Point ZoomStart = new Point();
        private Point ZoomEnd = new Point();
        private Point DragStart = new Point();
        private Point DragEnd = new Point();

        private int ZoomMargin = 0; //min. size of the zooming rectangle
        private int ZoomType = 0; //determines if the zoom is a box, or a vertical or horizontal section
        private Rectangle ZoomRect = new Rectangle(); //the rectangle that is drawn on screen during zoom region selection.
        private Matrix WtoDMatrix, DtoWMatrix; //translation matrices between world coordinates and device coordinates.

        private Line Xcur = new Line(); //cursor lines
        private Line Ycur = new Line();

        //legend display
        private FlowDocument FlowDoc = new FlowDocument();
        private TextBlock Legend = new TextBlock();

        //parameter to ensure that the zoomed out graph is completely visible and not completely "on the edge"
        private double ZoomoutMargin = 0.03;


        private Rect World = new Rect(); //contains the current "world" rectangle, i.e the rectangle of the world the user is using/seeing
        private Rect Device = new Rect(); //the "Device" rectangle, i.e. the world coordinates converted to pixels
        private Rect Zoom = new Rect();   //the stored world rectangle of user-set X- and Y- ranges.   

        //keep track of which zoom ranges were set by the user.
        private bool ZoomSet = false;
        private bool ZoomXSet = false;
        private bool ZoomYset = false;

        private int NrPointsInView = 0;

        //list of all graphs
        private List<XYGraph> AllGraphs = new List<XYGraph>();

        private WriteableBitmap wb;
        private Image img = new Image();

        private double MouseWheelZoomFactor;
        private int MaxNrPointsWithCaret;

        #endregion

        private class Rect
        {
            public double Xmin { get; set; } = 0;
            public double Xmax { get; set; } = 0;
            public double Ymin { get; set; } = 0;
            public double Ymax { get; set; } = 0;

            public void Copy(Rect R)
            {
                Xmin = R.Xmin;
                Xmax = R.Xmax;
                Ymin = R.Ymin;
                Ymax = R.Ymax;
            }

            public void Scale(double ScaleFactor)
            {
                double total_width = (Xmax - Xmin);
                double total_height = (Ymax - Ymin);

                Xmin = Xmin - total_width * ScaleFactor;
                Xmax = Xmax + total_width * ScaleFactor;

                Ymin = Ymin - total_height * ScaleFactor;
                Ymax = Ymax + total_height * ScaleFactor;
            }

            //mirror the rectangle in the X-axis:
            public void FlipY()
            {
                double temp = Ymin;
                Ymin = -Ymax;
                Ymax = -temp;
            }

            public Rect(double x_min, double x_max, double y_min, double y_max)
            {
                Xmin = x_min;
                Xmax = x_max;
                Ymin = y_min;
                Ymax = y_max;
            }

            public Rect() { }
        }

        private class XYGraph
        {
            public double[,] data; //"world data" Array 
            public int[] intdata;  //"device data" in integer pairs
            public int nrpoints;

            public Color color;
            public int thickness;
            public string name;
            public Run legendText;
            public bool drawlabel;

            public bool draw_caret;

            //constructor
            public XYGraph()
            {
                color = Colors.Black;
                thickness = 1;
                name = "graph1";
                drawlabel = true;
                draw_caret = false;
                legendText = new Run();
            }
        }

        /// <summary>
        /// Set the X range of the graph
        /// </summary>
        /// <param name="x_min">minimum X coordinate</param>
        /// <param name="x_max">maximum X coordinate</param>
        /// <returns>returns -1 if the min. x not smaller than the max. x</returns>
        public int SetRangeX(double x_min, double x_max)
        {
            if (x_min < x_max)
            {
                FindDataLimits("All");
                World.Xmin = x_min;
                World.Xmax = x_max;

                //save this zoom setting
                Zoom.Copy(World);
                ZoomSet = false;
                ZoomXSet = true;
                ZoomYset = false;

                return 0;
            }
            else return -1;
        }

        /// <summary>
        /// Set the Y range of the graph
        /// </summary>
        /// <param name="y_min">minimum Y coordinate</param>
        /// <param name="y_max">maximum Y coordinate</param>
        /// <returns>returns -1 if the min. Y is not smaller than the max. Y</returns>
        public int SetRangeY(double y_min, double y_max)
        {
            if (y_min < y_max)
            {
                FindDataLimits("All");
                World.Ymin = y_min;
                World.Ymax = y_max;

                //save this zoom setting
                Zoom.Copy(World);
                ZoomSet = false;
                ZoomXSet = false;
                ZoomYset = true;

                //prepare the world coordinates for use by mirrorring them in the X-axis
                World.FlipY();

                return 0;
            }
            else return -1;
        }

        /// <summary>
        /// Set the max. ranges of the graph
        /// </summary>
        /// <param name="x_min"></param>
        /// <param name="x_max"></param>
        /// <param name="y_min"></param>
        /// <param name="y_max"></param>
        /// <returns></returns>
        /// 
        public int SetRange(double x_min, double x_max, double y_min, double y_max)
        {
            if (x_min < x_max && y_min < y_max)
            {
                World = new Rect(x_min, x_max, y_min, y_max);

                //save this zoom setting
                Zoom.Copy(World);
                ZoomSet = true;
                ZoomXSet = false;
                ZoomYset = false;

                //prepare the world coordinates for use by mirrorring them in the X-axis
                World.FlipY();

                return 0;
            }
            else return -1; //incorrect limts
        }

        /// <summary>
        /// Add a graph to the collection using an array as the data source
        /// </summary>
        /// <param name="XYdata">double[,] array containing the data</param>
        /// <param name="nrPoints">Number of points used in the dataset</param>
        /// <param name="lineColor">Line color as System.Windows.Media.Color</param>
        /// <param name="name">Name string of the graph</param>
        /// <param name="drawLabel">Indicate whether this graph gets a label in the legend</param>
        /// <param name="drawCarets">Draw carets</param>
        /// <returns></returns>
        public int Add(double[,] XYdata, int nrPoints, Color lineColor, string name, bool drawLabel, bool drawCarets)
        {
            XYGraph graph = new XYGraph();

            if (XYdata != null) graph.data = XYdata; else return -1;
            int dim1 = XYdata.GetLength(0);
            graph.nrpoints = dim1;
            if (nrPoints <= dim1 && nrPoints > 0) graph.nrpoints = nrPoints; //prevent the user from entering an incorrect number of points
            graph.name = name;

            //allocate space for the int data buffer
            graph.intdata = new int[2 * graph.nrpoints];

            img.Source = wb;

            SolidColorBrush brush = new SolidColorBrush(lineColor);
            graph.drawlabel = drawLabel;

            graph.color = lineColor;

            graph.legendText.Text = name + " ";
            graph.legendText.Foreground = brush;

            graph.draw_caret = drawCarets;

            AllGraphs.Add(graph);
            return AllGraphs.Count;
        }

        /// <summary>
        /// The function for adding data from a list of objects to a graph
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="inData">List of objects containing the data to be graphed</param>
        /// <param name="X">The Func<T, double> that returns the x-coordinate from an object in the list</param>
        /// <param name="Y">The Func<T, double> that returns the y-coordinate from an object in the list</param>
        /// <param name="lineColor">The line color</param>
        /// <param name="name">The name of the data as displayed in the graph</param>
        /// <param name="drawLabel">This boolean determines if the name of the line is displayed</param>
        /// <param name="drawCarets">This boolean determines of carets are displayed on the line points</param>
        public int AddData<T>(List<T> inData, Func<T, double> X, Func<T, double> Y, Color lineColor, string name, bool drawLabel, bool drawCarets)
        {
            XYGraph graph = new XYGraph();
            if (inData == null || inData.Count == 0) return -1;
            graph.nrpoints = inData.Count;
            graph.data = null;
            graph.intdata = null;
            GC.Collect();

            graph.data = new double[graph.nrpoints, 2];
            int index = 0;
            foreach (var D in inData)
            {
                graph.data[index, 0] = X(D);
                graph.data[index++, 1] = Convert(Y(D));
            }
            graph.name = name;

            //allocate space for the int data buffer
            graph.intdata = new int[2 * graph.nrpoints];

            img.Source = wb;

            SolidColorBrush brush = new SolidColorBrush(lineColor);
            graph.drawlabel = drawLabel;

            graph.color = lineColor;

            graph.legendText.Text = name + " ";
            graph.legendText.Foreground = brush;

            graph.draw_caret = drawCarets;

            AllGraphs.Add(graph);
            return AllGraphs.Count;
        }

        /// <summary>
        /// Clear all graphs from the collection
        /// </summary>
        public void ClearAll()
        {
            foreach (var G in AllGraphs)
            {
                G.data = null;
                G.intdata = null;
            }
            AllGraphs = null;
            GC.Collect();
            AllGraphs = new List<XYGraph>();
            canvas.Children.Clear();
        }

        /// <summary>
        /// Force an update of the graph
        /// </summary>
        /// <param name="zoomout">Indicate whether to keep the current zoom setting . False: keep zoom, true: Zoom out</param>
        public void Update(bool zoomout)
        {
            if (zoomout)
            {
                if (ZoomSet)
                {
                    World.Copy(Zoom);
                    World.FlipY();
                }
                else if (ZoomXSet)
                {
                    FindDataLimits("All");
                    World.Xmin = Zoom.Xmin;
                    World.Xmax = Zoom.Xmax;
                }
                else if (ZoomYset)
                {
                    FindDataLimits("All");
                    World.Ymin = Zoom.Ymin;
                    World.Ymax = Zoom.Ymax;
                    World.FlipY();
                }
                else FindDataLimits("All");
            }
            if (ControlStarted == false || AllGraphs.Count == 0) return;

            ApplyTransformation();
            canvas.Children.Clear();
            wb.Clear();
            DrawLegend();
            //draw graphs
            foreach (var G in AllGraphs)
            {
                //draw lines
                wb.DrawPolyline(G.intdata, G.color);
                if (G.draw_caret)
                {
                    if (NrPointsInView < MaxNrPointsWithCaret)
                    {
                        for (int i = 0; i < G.nrpoints; i++)
                        {
                            int x = G.intdata[i * 2];
                            int y = G.intdata[i * 2 + 1];
                            if (x >= 0 && x <= Device.Xmax)
                            {
                                wb.DrawRectangle(x - 2, y - 2, x + 2, y + 2, G.color);
                            }
                        }
                    }
                }
            }

            // Add this image to the canvas
            img.Source = wb;
            Canvas.SetZIndex(img, -100);
            canvas.Children.Add(img);
        }

        /// <summary>
        /// Allow the user of the class to specify the size of the canvas.
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public void SetCanvasSize(double width, double height)
        {
            height = height - 20;
            canvas.Width = width;
            canvas.Height = height;

            //set device rectangle
            Device.Xmin = 0;
            Device.Xmax = width;
            Device.Ymin = 0;
            Device.Ymax = height;

            // Create the bitmap
            wb = new WriteableBitmap((int)width, (int)height, 96, 96, PixelFormats.Bgra32, null);

            if (ControlStarted == false)
            {
                ControlStarted = true;
                Update(true);
            }
            else Update(false);
        }

        #region Logscale Conversion functions
        private delegate double ConvertDelegate(double Input);
        private ConvertDelegate Convert;
        private ConvertDelegate ConvertBack;

        double ToLog(double d)
        {
            if (d > 0) return Math.Log10(d);
            else return double.MaxValue;
        }

        double ToLinear(double d)
        {
            return Math.Pow(10, d);
        }

        double NoConvert(double d) { return d; }

        /// <summary>
        /// Property for setting the graph to a logarithmix Y Axis
        /// </summary>
        private bool _Logscale;
        public bool LogScale
        {
            get { return _Logscale; }
            set
            {
                if (_Logscale != value)
                {
                    _Logscale = value;
                    if (_Logscale)
                    {
                        Convert = ToLog;
                        ConvertBack = ToLinear;
                        ConvertToLog();
                        if (ZoomSet || ZoomYset)
                        {
                            Zoom.Ymin = Convert(Zoom.Ymin);
                            Zoom.Ymax = Convert(Zoom.Ymax);
                        }
                        LogScale_menu.IsEnabled = false;
                        LinScale_menu.IsEnabled = true;
                        Update(true);
                    }
                    else
                    {
                        Convert = NoConvert;
                        ConvertBack = NoConvert;
                        ConvertToLin();
                        if (ZoomSet || ZoomYset)
                        {
                            Zoom.Ymin = ToLinear(Zoom.Ymin);
                            Zoom.Ymax = ToLinear(Zoom.Ymax);
                        }
                        LogScale_menu.IsEnabled = true;
                        LinScale_menu.IsEnabled = false;
                        Update(true);
                    }
                }
            }
        }


        void ConvertToLog()
        {
            foreach (var G in AllGraphs)
            {
                for (int i = 0; i < G.nrpoints; i++)
                {
                    G.data[i, 1] = ToLog(G.data[i, 1]);
                }
            }
        }

        void ConvertToLin()
        {
            foreach (var G in AllGraphs)
            {
                for (int i = 0; i < G.nrpoints; i++)
                {
                    G.data[i, 1] = ToLinear(G.data[i, 1]);
                }
            }
        }
        #endregion

        //find the extends of all world data so that all data can be seen in the graph
        void FindDataLimits(string name)
        {
            World.Xmin = 1e100;
            World.Xmax = -1e100;
            World.Ymin = 1e100;
            World.Ymax = -1e100;
            for (int j = 0; j < AllGraphs.Count; j++)
            {
                if (AllGraphs[j].name == name || name == "All")
                {
                    //search for the limits of the data
                    for (int i = 0; i < AllGraphs[j].nrpoints; i++)
                    {
                        if (AllGraphs[j].data[i, 0] > World.Xmax) World.Xmax = AllGraphs[j].data[i, 0];
                        if (AllGraphs[j].data[i, 0] < World.Xmin) World.Xmin = AllGraphs[j].data[i, 0];
                        if (AllGraphs[j].data[i, 1] > World.Ymax) World.Ymax = AllGraphs[j].data[i, 1];
                        if (AllGraphs[j].data[i, 1] < World.Ymin) World.Ymin = AllGraphs[j].data[i, 1];
                    }
                }
            }

            //Flip the y-coordinates of the world rectangle. The graph data will be flipped later
            World.FlipY();

            //make sure the graph is not stretched out to the very edge
            World.Scale(ZoomoutMargin);
        }

        // Prepare values for perform transformations.
        private void PrepareTransformations()
        {
            // Make WtoD.
            WtoDMatrix = Matrix.Identity;
            WtoDMatrix.Translate(-World.Xmin, -World.Ymin);
            try //make sure we don't divide by zero
            {
                double xscale = Device.Xmax / (World.Xmax - World.Xmin);
                double yscale = Device.Ymax / (World.Ymax - World.Ymin);
                WtoDMatrix.Scale(xscale, yscale);
                WtoDMatrix.Translate(Device.Xmin, Device.Ymin);
            }
            catch { }
            // Make DtoW.
            DtoWMatrix = WtoDMatrix;
            try
            {
                DtoWMatrix.Invert();
            }
            catch { }
        }

        //Convert the floating point world data to device data (image)
        private void ApplyTransformation()
        {
            PrepareTransformations();
            NrPointsInView = 0;
            //scale all graph data with the new view window settings
            for (int j = 0; j < AllGraphs.Count; j++)
            {
                Array.Clear(AllGraphs[j].intdata, 0, AllGraphs[j].intdata.Length);
                //search for the limits of the data
                for (int i = 0; i < AllGraphs[j].nrpoints; i++)
                {
                    double x = AllGraphs[j].data[i, 0];
                    double y = -AllGraphs[j].data[i, 1]; //flip the graph. The canvas has the zero in the upper left corner, not the lower left.

                    Point p = WtoDMatrix.Transform(new Point(x, y));
                    int xi = AllGraphs[j].intdata[i * 2] = (int)Math.Round(p.X, 0);
                    int yi = AllGraphs[j].intdata[i * 2 + 1] = (int)Math.Round(p.Y, 0);
                    if (xi >= 0 && xi <= Device.Xmax) NrPointsInView++;
                }
            }
        }

        //Draw the cursor on the canvas
        private void DrawCursor(Point loc, bool draw)
        {
            Point q = loc;
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Cross;
            try
            {
                canvas.Children.Remove(Xcur);
                canvas.Children.Remove(Ycur);
            }
            catch { }

            Xcur.X1 = 0;
            Xcur.Y1 = loc.Y;
            Xcur.X2 = canvas.Width;
            Xcur.Y2 = loc.Y;

            Ycur.X1 = loc.X;
            Ycur.Y1 = 0;
            Ycur.X2 = loc.X;
            Ycur.Y2 = canvas.Height;

            //calculate cursor position in world coordinates
            Point p = DtoWMatrix.Transform(q);

            //cursor coordinates text
            string sx = "", sy = "";
            string log = "";
            double xc = p.X;
            double yc = ConvertBack(-p.Y);

            if (Math.Abs(xc) < 0.1) sx = xc.ToString("E3"); else sx = xc.ToString("N3");
            if (Math.Abs(yc) < 0.1) sy = yc.ToString("E3"); else sy = yc.ToString("N3");
            if (_Logscale) log = "(Log scale)";
            cursor_text.Text = $"X: {sx}, Y: {sy} {log}";

            if (draw)
            {
                canvas.Children.Add(Xcur);
                canvas.Children.Add(Ycur);
                cursor_text.Visibility = Visibility.Visible;
            }
            else cursor_text.Visibility = Visibility.Hidden;
        }

        //draw the graph names in the legend
        private void DrawLegend()
        {
            if (AllGraphs.Count > 0)
            {
                FlowDoc.Blocks.Clear();
                Paragraph par = new Paragraph();
                foreach (var G in AllGraphs) par.Inlines.Add(G.legendText);
                FlowDoc.Blocks.Add(par);
                legend.Document = FlowDoc;
                legend.IsReadOnly = true;
            }
        }

        private void canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ZoomStart = Mouse.GetPosition(canvas);
        }

        private void canvas_MouseMove(object sender, MouseEventArgs e)
        {
            ZoomEnd = e.GetPosition(canvas);
            ZoomType = 0;
            if (Mouse.LeftButton == MouseButtonState.Pressed)
            {
                ZoomRect.Width = Math.Abs(ZoomEnd.X - ZoomStart.X);
                ZoomRect.Height = Math.Abs(ZoomEnd.Y - ZoomStart.Y);

                if (ZoomRect.Width >= ZoomMargin && ZoomRect.Height < ZoomMargin) ZoomType = 1;
                else if (ZoomRect.Width < ZoomMargin && ZoomRect.Height >= ZoomMargin) ZoomType = 2;
                else if (ZoomRect.Width >= ZoomMargin && ZoomRect.Height >= ZoomMargin) ZoomType = 3;

                if (ZoomType != 0) ZoomStarted = true;
            }
            else
            {
                ZoomStart = Mouse.GetPosition(canvas);
            }

            //first remove the zoomrect from view
            try { canvas.Children.Remove(ZoomRect); }
            catch { }
            if (ZoomStarted)
            {
                DrawCursor(ZoomEnd, false);
                if (ZoomType == 1)
                {
                    ZoomRect.Height = canvas.Height;
                    Canvas.SetLeft(ZoomRect, Math.Min(ZoomEnd.X, ZoomStart.X));
                    Canvas.SetTop(ZoomRect, 0);
                    canvas.Children.Add(ZoomRect);
                }
                else if (ZoomType == 2)
                {
                    ZoomRect.Width = canvas.Width;
                    Canvas.SetLeft(ZoomRect, 0);
                    Canvas.SetTop(ZoomRect, Math.Min(ZoomEnd.Y, ZoomStart.Y));
                    canvas.Children.Add(ZoomRect);
                }
                else if (ZoomType == 3)
                {
                    Canvas.SetLeft(ZoomRect, Math.Min(ZoomEnd.X, ZoomStart.X));
                    Canvas.SetTop(ZoomRect, Math.Min(ZoomEnd.Y, ZoomStart.Y));
                    canvas.Children.Add(ZoomRect);
                }
            }
            else
            {
                DrawCursor(ZoomEnd, true);
            }
            if (DragStarted)
            {
                if (Mouse.RightButton == MouseButtonState.Pressed)
                {
                    DragEnd = Mouse.GetPosition(canvas);

                    Point w1 = DtoWMatrix.Transform(DragStart);
                    Point w2 = DtoWMatrix.Transform(DragEnd);

                    Point w = new Point();
                    w.X = -w1.X + w2.X;
                    w.Y = -w1.Y + w2.Y;

                    World.Xmin = World.Xmin - w.X;
                    World.Xmax = World.Xmax - w.X;

                    World.Ymin = World.Ymin - w.Y;
                    World.Ymax = World.Ymax - w.Y;

                    //recalculate the transformation matrices
                    PrepareTransformations();
                    //show graph
                    Update(false);

                    DragStart = DragEnd;
                }
                else DragStarted = false;
            }
        }

        private void canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //remove zoom rectangle
            if (ZoomStarted)
            {
                ZoomStarted = false;
                try { canvas.Children.Remove(ZoomRect); }
                catch { }
                double x1 = 0;
                double y1 = 0;
                double x2 = 0;
                double y2 = 0;
                //zoom graph
                PrepareTransformations(); //make sure the current matrices are calculated

                switch (ZoomType)
                {
                    case 0:
                        return;
                    case 1: //vertical scrolling window
                        x1 = Math.Min(ZoomStart.X, ZoomEnd.X);
                        y1 = 0;
                        x2 = Math.Max(ZoomStart.X, ZoomEnd.X);
                        y2 = canvas.Height;
                        break;
                    case 2://horizontal scrolling window
                        x1 = 0;
                        y1 = Math.Min(ZoomStart.Y, ZoomEnd.Y);
                        x2 = canvas.Width;
                        y2 = Math.Max(ZoomStart.Y, ZoomEnd.Y);
                        break;
                    case 3: //standard square zooming rectangle
                        x1 = Math.Min(ZoomStart.X, ZoomEnd.X);
                        y1 = Math.Min(ZoomStart.Y, ZoomEnd.Y);
                        x2 = Math.Max(ZoomStart.X, ZoomEnd.X);
                        y2 = Math.Max(ZoomStart.Y, ZoomEnd.Y);
                        break;
                    default:
                        return;
                }
                //calculate what these coordinates are in world coordinates
                Point w1 = DtoWMatrix.Transform(new Point(x1, y1));
                Point w2 = DtoWMatrix.Transform(new Point(x2, y2));

                //set the new world coordinates to the new values
                World.Xmin = w1.X; World.Xmax = w2.X;
                World.Ymin = w1.Y; World.Ymax = w2.Y;

                PrepareTransformations();//recalculate the transformation matrices

                //show graph
                Update(false);
                ZoomStarted = false;
            }
        }

        private void canvas_MouseLeave(object sender, MouseEventArgs e)
        {
            DrawCursor(new Point(0, 0), false);
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Arrow;
            ZoomStarted = false;
            canvas.Children.Remove(ZoomRect);
        }

        private void canvas_MouseEnter(object sender, MouseEventArgs e)
        {
            ZoomStarted = false;
        }

        private void canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragStart = Mouse.GetPosition(canvas);
            DragStarted = true;
        }

        private void canvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            DragStarted = false;
        }

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 0 || e.NewSize.Height <= 0) return;
            SetCanvasSize(e.NewSize.Width, e.NewSize.Height);
        }

        private void reset_Click(object sender, RoutedEventArgs e)
        {
            Update(true);
        }

        private void LogScale_menu_Click(object sender, RoutedEventArgs e)
        {
            LogScale = true;
        }

        private void SaveData_menu_Click(object sender, RoutedEventArgs e)
        {
            var S = new List<List<string>>();
            foreach (var G in AllGraphs)
            {
                var D = new List<string>();
                for (int i=0; i<D.Count; i++) D.Add($"{G.data[i,0]},{G.data[i,1]}");
                S.Add(D);
            }
        }

        private void SaveImage_menu_Click(object sender, RoutedEventArgs e)
        {

        }

        private void LinScale_menu_Click(object sender, RoutedEventArgs e)
        {
            LogScale = false;
        }

        private void canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            int movement = e.Delta;
            if (movement < 0)
            {
                double xrange = Math.Abs(World.Xmax - World.Xmin) * MouseWheelZoomFactor;
                double yrange = Math.Abs(World.Ymax - World.Ymin) * MouseWheelZoomFactor;

                World.Xmin = World.Xmin - xrange;
                World.Xmax = World.Xmax + xrange;
                World.Ymin = World.Ymin - yrange;
                World.Ymax = World.Ymax + yrange;
                Update(false);
            }
            else
            {
                double xrange = Math.Abs(World.Xmax - World.Xmin) * 0.1;
                double yrange = Math.Abs(World.Ymax - World.Ymin) * 0.1;

                World.Xmin = World.Xmin + xrange;
                World.Xmax = World.Xmax - xrange;
                World.Ymin = World.Ymin + yrange;
                World.Ymax = World.Ymax - yrange;
                Update(false);
            }

        }
    }
}

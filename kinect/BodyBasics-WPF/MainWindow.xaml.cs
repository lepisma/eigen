﻿//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.BodyBasics
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using System.Timers;

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Radius of drawn hand circles
        /// </summary>
        private const double HandSize = 30;

        private Random rgen = new Random();
        double[] rgenArray = new double[23];

        private double globalVolume = 0;
        private double globalTempo = 0;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Constant for clamping Z values of camera space points from being negative
        /// </summary>
        private const float InferredZPositionClamp = 0.1f;

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as closed
        /// </summary>
        private readonly Brush handClosedBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));

        /// Custom brushes
        private readonly Brush backgroundBrush = new SolidColorBrush(Color.FromArgb(255, 37, 35, 36));
        private readonly Brush themeBrushDark = new SolidColorBrush(Color.FromArgb(255, 47, 193, 47));
        private readonly Brush themeBrushLight = new SolidColorBrush(Color.FromArgb(76, 47, 193, 47));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as opened
        /// </summary>
        private readonly Brush handOpenBrush = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as in lasso (pointer) position
        /// </summary>
        private readonly Brush handLassoBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Drawing group for body rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Coordinate mapper to map one type of point to another
        /// </summary>
        private CoordinateMapper coordinateMapper = null;

        /// <summary>
        /// Reader for body frames
        /// </summary>
        private BodyFrameReader bodyFrameReader = null;

        /// <summary>
        /// Array for the bodies
        /// </summary>
        private Body[] bodies = null;

        /// <summary>
        /// definition of bones
        /// </summary>
        private List<Tuple<JointType, JointType>> bones;

        /// <summary>
        /// Width of display (depth space)
        /// </summary>
        private int displayWidth;

        /// <summary>
        /// Height of display (depth space)
        /// </summary>
        private int displayHeight;

        /// <summary>
        /// List of colors for each body tracked
        /// </summary>
        private List<Pen> bodyColors;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            // one sensor is currently supported
            this.kinectSensor = KinectSensor.GetDefault();

            // get the coordinate mapper
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            // get the depth (display) extents
            FrameDescription frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            // get size of joint space
            this.displayWidth = frameDescription.Width;
            this.displayHeight = frameDescription.Height;

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            // a bone defined as a line between two joints
            this.bones = new List<Tuple<JointType, JointType>>();

            // Torso
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Head, JointType.Neck));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Neck, JointType.SpineShoulder));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.SpineMid));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineMid, JointType.SpineBase));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipLeft));

            // Right Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandRight, JointType.HandTipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.ThumbRight));

            // Left Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderLeft, JointType.ElbowLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowLeft, JointType.WristLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.HandLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandLeft, JointType.HandTipLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.ThumbLeft));

            // Right Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipRight, JointType.KneeRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeRight, JointType.AnkleRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleRight, JointType.FootRight));
            // Left Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipLeft, JointType.KneeLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeLeft, JointType.AnkleLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleLeft, JointType.FootLeft));

            // populate body colors, one for each BodyIndex
            this.bodyColors = new List<Pen>();

            this.bodyColors.Add(new Pen(Brushes.Red, 6));
            this.bodyColors.Add(new Pen(Brushes.Orange, 6));
            this.bodyColors.Add(new Pen(Brushes.Green, 6));
            this.bodyColors.Add(new Pen(Brushes.Blue, 6));
            this.bodyColors.Add(new Pen(Brushes.Indigo, 6));
            this.bodyColors.Add(new Pen(Brushes.Violet, 6));

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();

            // Initialize random array

            for(int i = 0; i < 23; i ++){
                rgenArray[i] = rgen.NextDouble();
            }
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.imageSource;
            }
        }

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Execute start up tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                this.bodyFrameReader.FrameArrived += this.Reader_FrameArrived;
            }
            EigenRequest.playMusic("9");
            EigenRequest.changeVolume("9", "0.3"); // Background sound
        }

        public static int right_select = -1;

        public static void right_part(float angle)
        {
            rip_ok = 1;
            
            if(angle<=180&&angle>=120&&right_select!=0)
            {
                Console.Out.WriteLine(" section " + 1 + " angle " + angle);
                EigenRequest.startLoop("0");
                right_select = 0;
                rip_ok = 0;
            }
            else if (angle < 120 && angle >= 60&&right_select!=1)
            {
                Console.Out.WriteLine(" section " + 2 + " angle " + angle);
                EigenRequest.startLoop("1");
                right_select = 1;
                rip_ok = 0;
            }
            else if (angle < 60 && angle >= 0&&right_select!=2)
            {
                Console.Out.WriteLine(" section " + 3 + " angle " + angle);
                EigenRequest.startLoop("2");
                right_select = 2;
                rip_ok = 0;
            }

            rip_ok = 0;

        }

        public static float AngleBetweenJoints(Body body, JointType centerJoint, JointType topJoint, JointType bottomJoint)
        {
            Vector3 centerJointCoord = new Vector3(body.Joints[centerJoint].Position.X, body.Joints[centerJoint].Position.Y, body.Joints[centerJoint].Position.Z);
            Vector3 topJointCoord = new Vector3(body.Joints[topJoint].Position.X, body.Joints[topJoint].Position.Y, body.Joints[topJoint].Position.Z);
            Vector3 bottomJointCoord = new Vector3(body.Joints[bottomJoint].Position.X, body.Joints[bottomJoint].Position.Y, body.Joints[bottomJoint].Position.Z);

            Vector3 firstVector = (bottomJointCoord - centerJointCoord);
            Vector3 secondVector = (topJointCoord - centerJointCoord);

            return AngleBetweenTwoVectors(firstVector, secondVector);
        }

        public static float AngleBetweenTwoVectors(Vector3 vectorA, Vector3 vectorB)
        {
            vectorA.Normalize();
            vectorB.Normalize();

            float dotProduct = 0.0f;

            dotProduct = Vector3.Dot(vectorA, vectorB);

            return (float)Math.Round((Math.Acos(dotProduct) * 180 / Math.PI), 2);
           
        }

        public static float max_depth = 0;
        public static float depth_from_Hand(Body body, JointType HandLeft, JointType HandRight)
        {

             float depth =Math.Abs(body.Joints[HandLeft].Position.X - body.Joints[HandRight].Position.X);
             // Console.Out.WriteLine("11 " + depth);
             return depth;
        }

        public static float depth_from_Hand_Y(Body body, JointType HandLeft, JointType HandRight)
        {

            float depth = Math.Abs(body.Joints[HandLeft].Position.Y - body.Joints[HandRight].Position.Y);
            // Console.Out.WriteLine("11 " + depth);
            return depth;
        }

        //code for jump from right leg
        public static Boolean up = false,down=false;

        public static Boolean jump_right_leg(Body body, JointType FootRight ,JointType FootLeft)
        {
            float rightleg = -1*body.Joints[FootRight].Position.Y;
            float leftleg = -1 * body.Joints[FootLeft].Position.Y;
           //Console.Out.WriteLine("r= " + rightleg * 100 + " l= " + leftleg * 100 + " diff " + (leftleg - rightleg )*100);

            if ((( leftleg - rightleg)*100 )> 20 && up == false ) { 
                up = true; //Console.Out.WriteLine("up");
                return false; }
            
            else if (up == true && ((leftleg - rightleg) * 100) < 10) { up = false;  return true; }

            else  return false;
        }
        public static Boolean jump_left_leg(Body body, JointType FootRight, JointType FootLeft)
        {
            float rightleg = -1 * body.Joints[FootRight].Position.Y;
            float leftleg = -1 * body.Joints[FootLeft].Position.Y;
        //    Console.Out.WriteLine("r= " + rightleg * 100 + " l= " + leftleg * 100 + " diff " + (rightleg - leftleg) * 100);

            if (((rightleg - leftleg) * 100) > 20 && up == false)
            {
                up = true; Console.Out.WriteLine("up"); return false;
            }

            else if (up == true && ((rightleg - leftleg) * 100) < 10) { up = false; return true; }

            else return false;
        }


      public static  float pre_z = 0;
      public static Stopwatch sw = Stopwatch.StartNew();
      public static int rip_ok=0; 
        
        public static void rip(Body body, JointType  ElbowLeft, JointType WristLeft, JointType WristRight,JointType ShoulderLeft)
      {
          if (rip_ok==0) {
              if ((body.Joints[ElbowLeft].Position.Y < body.Joints[WristLeft].Position.Y) && (body.Joints[ElbowLeft].Position.Y < body.Joints[ShoulderLeft].Position.Y))
              {
                  //  float valuerip = (body.Joints[WristRight].Position.Z) / Math.Abs(body.Joints[WristLeft].Position.Y - body.Joints[ElbowLeft].Position.Y);
                  //// Console.Out.WriteLine("rip " + Math.Abs(pre_z - body.Joints[WristRight].Position.Z)*1000);

                  //  float normalisation_factor=(body.Joints[JointType.Neck].Position.Y -body.Joints[JointType.SpineMid].Position.Y);

                  //  Console.Out.WriteLine("rip required" + Math.Abs(pre_z - body.Joints[WristRight].Position.Z)*1000 / normalisation_factor);

                  //  if (Math.Abs(pre_z-body.Joints[WristRight].Position.Z)*1000>8)
                  //  { //EigenRequest.playMusic("3");
                  //      //Console.Out.WriteLine("rip required" + Math.Abs(pre_z - body.Joints[WristRight].Position.Z)/normalisation_factor);
                  //  }
                  //  pre_z = body.Joints[WristRight].Position.Z;

                  if (sw.ElapsedMilliseconds >= 1500)
                  {
                      EigenRequest.playMusic("4"); // RIP
                      
                      sw.Stop();
                      sw.Reset();
                      sw.Start();
                  }
              }
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.bodies == null)
                    {
                        this.bodies = new Body[bodyFrame.BodyCount];
                    }

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects wi  ll be re-used.
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }



            if (dataReceived)
            {
                using (DrawingContext dc = this.drawingGroup.Open())
                {
                    // Draw a transparent background to set the render size
                    dc.DrawRectangle(backgroundBrush, null, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
                    this.DrawMainCircle(dc);

                    int penIndex = 0;
                    foreach (Body body in this.bodies)
                    {
                        Pen drawPen = this.bodyColors[penIndex++];

                        if (body.IsTracked)
                        {
                            //=================================================================================================================//
                            //angle between three point
                            //     float angle=   AngleBetweenJoints(body, JointType.ElbowRight, JointType.HandRight, JointType.ShoulderRight);

                            //============================================================================================//
                            //select section from right side

                            Boolean select = false;
                            Joint head = body.Joints[JointType.Head];
                            Joint Wristleft = body.Joints[JointType.WristLeft];

                            float right_section = AngleBetweenJoints(body, JointType.ShoulderRight, JointType.ElbowRight, JointType.HipRight);

                            int count = 1;
                            if (head.Position.Y < Wristleft.Position.Y)
                            {
                                if (count == 1) { select = true; count = 2; }
                                else if (count == 2) { select = false; count = 1; }
                            }

                            if (select && count == 2) {
   if ((body.Joints[JointType.ShoulderLeft].Position.Y < body.Joints[JointType.ElbowLeft].Position.Y) &&
       (body.Joints[JointType.ElbowLeft].Position.Y < body.Joints[JointType.WristLeft].Position.Y))

   { right_part(right_section); rip_ok = 0; }
                            }

                            //=======================================================================================//
                            //find distance from z axis
                            // if (body.Joints[JointType.WristLeft].Position.Y>body.Joints[JointType.ElbowLeft].Position.Y)
                            {

                                float depth_hand = depth_from_Hand(body, JointType.HandLeft, JointType.HandRight);
                                float depth_hand_Y = depth_from_Hand_Y(body, JointType.HandLeft, JointType.HandRight);

                                Console.Out.WriteLine(right_select.ToString());
                                this.globalVolume = depth_hand;
                                this.globalTempo = depth_hand_Y;
                                EigenRequest.changeVolume(right_select.ToString(), depth_hand.ToString());

                                float bmp = depth_hand_Y * 360;
                                if (bmp <= 0) bmp = 1;
                                EigenRequest.changeBPM(right_select.ToString(), 
                                    (System.Convert.ToUInt32(bmp)
                                    ).ToString());
                                Console.Out.WriteLine((System.Convert.ToUInt32(depth_hand_Y * 360)
                                    ).ToString());
                            }

                            // detect jump
                            Boolean jumpright = jump_right_leg(body, JointType.FootRight, JointType.FootLeft);
                            Boolean jumpleft = jump_left_leg(body, JointType.FootRight, JointType.FootLeft);
                            //  Console.Out.WriteLine(jump);
                            if (jumpright)
                            {

                                //Console.Out.WriteLine("jumpright");
                                EigenRequest.playMusic("6");

                                //Console.Out.WriteLine("jump");
                                //EigenRequest.playMusic("6");

                            }
                            if (jumpleft)
                            {
                                //Console.Out.WriteLine("jumpleft");
                                EigenRequest.playMusic("7");
                            }
                            //play rip 
                            rip(body, JointType.ElbowLeft, JointType.WristLeft, JointType.WristRight, JointType.ShoulderLeft);
                            {

                                // Console.Out.WriteLine(angle);
                                // Console.Out.WriteLine(right_section);


                                this.DrawClippedEdges(body, dc);

                                IReadOnlyDictionary<JointType, Joint> joints = body.Joints;

                                // convert the joint points to depth (display) space
                                Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();

                                foreach (JointType jointType in joints.Keys)
                                {
                                    // sometimes the depth(Z) of an inferred joint may show as negative
                                    // clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
                                    CameraSpacePoint position = joints[jointType].Position;
                                    if (position.Z < 0)
                                    {
                                        position.Z = InferredZPositionClamp;
                                    }

                                    DepthSpacePoint depthSpacePoint = this.coordinateMapper.MapCameraPointToDepthSpace(position);
                                    jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                                }

                                this.DrawBody(joints, jointPoints, dc, drawPen);

                                //this.DrawHand(body.HandLeftState, jointPoints[JointType.HandLeft], dc);
                                //this.DrawHand(body.HandRightState, jointPoints[JointType.HandRight], dc);
                                this.DrawBars(dc, globalTempo, globalVolume);
                                this.DrawRandombars(dc);
                            }
                        }

                        // prevent drawing outside of our render area
                        this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
                    }
                }
            }
        }

        /// <summary>
        /// Draws a body
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="drawingPen">specifies color to draw a specific body</param>
        private void DrawBody(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {
            // Draw the bones
            foreach (var bone in this.bones)
            {
                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, drawingPen);
            }

            // Draw the joints
            foreach (JointType jointType in joints.Keys)
            {
                Brush drawBrush = null;

                TrackingState trackingState = joints[jointType].TrackingState;

                if (trackingState == TrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (trackingState == TrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    if ((jointType == JointType.HandTipLeft) || (jointType == JointType.HandTipRight))
                    {
                        drawingContext.DrawEllipse(Brushes.Orange, null, jointPoints[jointType], 13, 13);
                        drawingContext.DrawEllipse(backgroundBrush, null, jointPoints[jointType], 9, 9);
                    }
                    //drawingContext.DrawEllipse(drawBrush, null, jointPoints[jointType], JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Draws one bone of a body (joint to joint)
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="jointType0">first joint of bone to draw</param>
        /// <param name="jointType1">second joint of bone to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// /// <param name="drawingPen">specifies color to draw a specific bone</param>
        private void DrawBone(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, DrawingContext drawingContext, Pen drawingPen)
        {
            Joint joint0 = joints[jointType0];
            Joint joint1 = joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == TrackingState.NotTracked ||
                joint1.TrackingState == TrackingState.NotTracked)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
            {
                drawPen = drawingPen;
            }

            // drawingContext.DrawLine(drawPen, jointPoints[jointType0], jointPoints[jointType1]);
        }

        /// <summary>
        /// Draws a hand symbol if the hand is tracked: red circle = closed, green circle = opened; blue circle = lasso
        /// </summary>
        /// <param name="handState">state of the hand</param>
        /// <param name="handPosition">position of the hand</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        //private void DrawHand(HandState handState, Point handPosition, DrawingContext drawingContext)
        //{
        //    switch (handState)
        //    {
        //        case HandState.Closed:
        //            drawingContext.DrawEllipse(this.handClosedBrush, null, handPosition, HandSize, HandSize);
        //            break;

        //        case HandState.Open:
        //            drawingContext.DrawEllipse(this.handOpenBrush, null, handPosition, HandSize, HandSize);
        //            break;

        //        case HandState.Lasso:
        //            drawingContext.DrawEllipse(this.handLassoBrush, null, handPosition, HandSize, HandSize);
        //            break;
        //    }
        //}

        /// <summary>
        /// Draws indicators to show which edges are clipping body data
        /// </summary>
        /// <param name="body">body to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawClippedEdges(Body body, DrawingContext drawingContext)
        {
            FrameEdges clippedEdges = body.ClippedEdges;

            if (clippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, this.displayHeight - ClipBoundsThickness, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, this.displayHeight));
            }

            if (clippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(this.displayWidth - ClipBoundsThickness, 0, ClipBoundsThickness, this.displayHeight));
            }
        }

        // Draws vertical bars for tools
        private void DrawBars(DrawingContext context, double beatValue, double volumeValue)
        {
            if (beatValue > 1)
            {
                beatValue = 1;
            }
            if (volumeValue > 1)
            {
                volumeValue = 1;
            }
            DrawBeatBar(context, beatValue);
            DrawVolumeBar(context, volumeValue);
        }

        private void DrawMainCircle(DrawingContext context)
        {
            double centerX = this.displayHeight / 2.0;
            double centerY = this.displayWidth / 2.0;

            double radius = this.displayWidth / 10.0;
            centerX += radius;
            centerY -= 2.4 * radius;
            Point center = new Point(centerX, centerY);

            context.DrawEllipse(themeBrushLight, null, center, radius * 2, radius * 2);
            radius -= 7;
            context.DrawEllipse(backgroundBrush, null, center, radius * 2, radius * 2);
            radius -= 7;
            context.DrawEllipse(themeBrushDark, null, center, radius * 2, radius * 2);
            radius -= 7;
            context.DrawEllipse(backgroundBrush, null, center, radius * 2, radius * 2);

        }

        // Draw volume bar on the top edge
        private void DrawVolumeBar(DrawingContext context, double value)
        {
            double rectWidth = ClipBoundsThickness;
            double rectHeight = 0.5 * this.displayHeight;
            double rectCurrentHeight = value * rectHeight;

            double startX = rectWidth * 3;
            double startY = rectWidth * 3;
            double startCurrentY = startY + (rectHeight - rectCurrentHeight);

            context.DrawRectangle(
                themeBrushLight,
                null,
                new Rect(startX, startY, rectWidth, rectHeight));

            context.DrawRectangle(
                themeBrushDark,
                null,
                new Rect(startX, startCurrentY, rectWidth, rectCurrentHeight));
        }

        // Draw tempo bar on the bottom edge
        private void DrawBeatBar(DrawingContext context, double value)
        {
            double rectWidth = ClipBoundsThickness;
            double rectHeight = 0.5 * this.displayHeight;
            double rectCurrentHeight = value * rectHeight;

            double startX = this.displayWidth - (rectWidth * 4);
            double startY = rectWidth * 3;
            double startCurrentY = startY + (rectHeight - rectCurrentHeight);

            context.DrawRectangle(
                themeBrushLight,
                null,
                new Rect(startX, startY, rectWidth, rectHeight));

            context.DrawRectangle(
                themeBrushDark,
                null,
                new Rect(startX, startCurrentY, rectWidth, rectCurrentHeight));
        }

        private void DrawRandombars(DrawingContext context)
        {
            // Draw for left and right side
            double totalHeight = 0.2 * this.displayHeight;
            double originX = ClipBoundsThickness * 1;
            double originY = this.displayHeight - totalHeight;
            double gap = ClipBoundsThickness * 0.1;

            double width = ClipBoundsThickness * 0.7;

            for (int i = 0; i < 23; i++)
            {
                double startX = (i + 1) * originX + i * gap;
                double newRand = ((rgenArray[i] * 0.7) + (rgen.NextDouble() * 0.3));
                double height = newRand * totalHeight;
                rgenArray[i] = newRand;
                double startY = originY + totalHeight - height;

                // Draw four random bars
                context.DrawRectangle(
                    themeBrushDark,
                    null,
                    new Rect(startX, startY, width, height));

                startX = this.displayWidth - startX - width;

                context.DrawRectangle(
                    themeBrushDark,
                    null,
                    new Rect(startX, startY, width, height));
            }

            // Draw horizontal bars for breaking the bars
            for (int i = 0; i < 5; i++)
            {
                double startX = 0;
                double height = ClipBoundsThickness * 0.5;
                double startY = this.displayHeight - (i + 1) * height - (i + 1) * gap * 3;

                context.DrawRectangle(
                    backgroundBrush,
                    null,
                    new Rect(startX, startY, this.displayWidth, height));
            }
        }


        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }
    }
}

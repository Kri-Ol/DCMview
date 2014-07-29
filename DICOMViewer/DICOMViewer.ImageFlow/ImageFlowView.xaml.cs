using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using System.Xml.Linq;

// Implementation of the Image Flow View using Windows Presentation Foundation (WPF).
// Major parts of the code have been taken from the 'WPF Cover Flow Tutorial':
// http://d3dal3.blogspot.com/2008/10/wpf-cover-flow-tutorial-part-1.html

namespace DICOMViewer.ImageFlow
{
    public partial class ImageFlowView : Window
    {
        private DispatcherTimer mSliderDelayTimer = new DispatcherTimer();
        private DispatcherTimer mAnimationTimer = new DispatcherTimer();
        private DispatcherTimer mUserInputBlockTimer = new DispatcherTimer();
        private int mCurrentViewPosition = 0;

        private readonly TimeSpan mAnimationDuration = TimeSpan.FromMilliseconds(600);
        private readonly int HalfPageSize = 5;
        private readonly double StepSize = 0.1;
        private readonly Dictionary<int, ImageSlice> mImageSliceList = new Dictionary<int, ImageSlice>();
 
        public ImageFlowView()
        {
            InitializeComponent();

            this.KeyDown += new KeyEventHandler(this.UserControl_KeyDown);
            this.KeyUp += new KeyEventHandler(this.UserControl_KeyUp);
 
            mAnimationTimer.Interval = mAnimationDuration;
            mAnimationTimer.Tick += new EventHandler(AnimationTimerEventHandler);

            mSliderDelayTimer.Interval = TimeSpan.FromMilliseconds(100);
            mSliderDelayTimer.Tick += new EventHandler(SliderDelayTimerEventHandler);

            mUserInputBlockTimer.Interval = TimeSpan.FromMilliseconds(10);
            mUserInputBlockTimer.Tick += new EventHandler(UserInputBlockTimerEventHandler);
        }

        public void MoveToNextImage()
        {
            if (mCurrentViewPosition < mImageSliceList.Count - 1)
            {
                if (mImageSliceList.ContainsKey(mCurrentViewPosition - HalfPageSize))
                    mImageSliceList[mCurrentViewPosition - HalfPageSize].ResetBitmap();

                if (mImageSliceList.ContainsKey(mCurrentViewPosition + HalfPageSize))
                    mImageSliceList[mCurrentViewPosition + HalfPageSize].SetBitmap();

                TransformImageSlice(mCurrentViewPosition, mCurrentViewPosition + HalfPageSize);

                // Increment current ViewPosition
                mCurrentViewPosition++;

                // ImageSlice, which has the focus, has to be moved to the background
                AnimateImageSlice(mCurrentViewPosition, mCurrentViewPosition - 1);

                // Next ImageSlice has to be brought to the foreground 
                AnimateImageSlice(mCurrentViewPosition, mCurrentViewPosition);

                // Move Camera to new position
                mCamera.Position = new Point3D(StepSize * mCurrentViewPosition, mCamera.Position.Y, mCamera.Position.Z);

                // Update Info Label
                UpdateInfoLabel();

                // Update Slider
                Slider.Value = mCurrentViewPosition;

                // Start Animation Timer to block further user input until animation is done
                mAnimationTimer.Start();
            }
        }

        public void MoveToPreviousImage()
        {
            if (mCurrentViewPosition > 0)
            {
                if (mImageSliceList.ContainsKey(mCurrentViewPosition + HalfPageSize))
                    mImageSliceList[mCurrentViewPosition + HalfPageSize].ResetBitmap();

                if (mImageSliceList.ContainsKey(mCurrentViewPosition - HalfPageSize))
                    mImageSliceList[mCurrentViewPosition - HalfPageSize].SetBitmap();

                TransformImageSlice(mCurrentViewPosition, mCurrentViewPosition - HalfPageSize);

                // Decrement current ViewPosition
                mCurrentViewPosition--;

                // ImageSlice, which has the focus, has to be moved to the background
                AnimateImageSlice(mCurrentViewPosition, mCurrentViewPosition + 1);

                // Next ImageSlice has to be brought to the foreground 
                AnimateImageSlice(mCurrentViewPosition, mCurrentViewPosition);

                // Move Camera to new position
                mCamera.Position = new Point3D(StepSize * mCurrentViewPosition, mCamera.Position.Y, mCamera.Position.Z);

                // Update Info Label
                UpdateInfoLabel();

                // Update Slider
                Slider.Value = mCurrentViewPosition;

                // Start Animation Timer to block further user input until animation is done
                mAnimationTimer.Start();
            }
        }

        public void AddImageSlice(XDocument theXDocument, string theFileName, string theZValue)
        {
            ImageSlice newImageSlice = new ImageSlice(theXDocument, theFileName, theZValue, the3DModel);

            // Insert new Image Slice at the end
            mImageSliceList.Add(mImageSliceList.Count, newImageSlice);
        }

        public void PostInitialize()
        {
            mCurrentViewPosition = mImageSliceList.Count / 2;

            // Update min/max value of Slider
            Slider.Minimum = 0;
            Slider.Maximum = mImageSliceList.Count - 1;

            ShowImage(mCurrentViewPosition);
            ShowDefocusedImages();
        }

        // Moves slice (with index equal to 'theSliceIndex') to it's new view position
        // Movement is done animated
        private void AnimateImageSlice(int theViewPosition, int theSliceIndex)
        {
            double aRotationAngle = RotationAngle(theViewPosition, theSliceIndex);
            double aTranslationX = TranslationX(theViewPosition, theSliceIndex);
            double aTranslationY = TranslationY(theViewPosition, theSliceIndex);
            double aTranslationZ = TranslationZ(theViewPosition, theSliceIndex);

            if (mImageSliceList.ContainsKey(theSliceIndex))
                mImageSliceList[theSliceIndex].Animate(aRotationAngle, aTranslationX, aTranslationY, aTranslationZ, mAnimationDuration);
        }

        // Moves slice (with index equal to 'theSliceIndex') to it's new view position
        // Movement is done without animation (transformation only)
        private void TransformImageSlice(int theViewPosition, int theSliceIndex)
        {
            double aRotationAngle = RotationAngle(theViewPosition, theSliceIndex);
            double aTranslationX = TranslationX(theViewPosition, theSliceIndex);
            double aTranslationY = TranslationY(theViewPosition, theSliceIndex);
            double aTranslationZ = TranslationZ(theViewPosition, theSliceIndex);

            if (mImageSliceList.ContainsKey(theSliceIndex))
                mImageSliceList[theSliceIndex].Transform(aRotationAngle, aTranslationX, aTranslationY, aTranslationZ);
        }

        private double RotationAngle(int aViewPosition, int aSlicePosition)
        {
            return Math.Sign(aSlicePosition - aViewPosition) * -90;
        }

        private double TranslationX(int aViewPosition, int aSlicePosition)
        {
            return aSlicePosition * StepSize + Math.Sign(aSlicePosition - aViewPosition) * 3.5;
        }

        private double TranslationY(int aViewPosition, int aSlicePosition)
        {
            return 0;
        }

        private double TranslationZ(int aViewPosition, int aSlicePosition)
        {
            return aSlicePosition == aViewPosition ? 1 : -3;
        }

        private void RemoveDefocusedImages()
        {
            for (int i = mCurrentViewPosition - HalfPageSize - 10; i < mCurrentViewPosition + HalfPageSize + 10; i++)
            {
                if (i != mCurrentViewPosition)
                {
                    if (mImageSliceList.ContainsKey(i))
                        mImageSliceList[i].ResetBitmap();
                }
            }
        }

        private void ShowDefocusedImages()
        {
            for (int i = mCurrentViewPosition - HalfPageSize; i < mCurrentViewPosition + HalfPageSize; i++)
            {
                if (i != mCurrentViewPosition)
                {
                    if (mImageSliceList.ContainsKey(i))
                        mImageSliceList[i].SetBitmap();

                    TransformImageSlice(mCurrentViewPosition, i);
                }
            }
        }

        private void ShowImage(int newPosition)
        {
            if (newPosition < 0 || newPosition > mImageSliceList.Count - 1)
                return;

            if (mImageSliceList.ContainsKey(mCurrentViewPosition))
                mImageSliceList[mCurrentViewPosition].ResetBitmap();

            mCurrentViewPosition = newPosition;

            if (mImageSliceList.ContainsKey(mCurrentViewPosition))
                mImageSliceList[mCurrentViewPosition].SetBitmap();

            TransformImageSlice(mCurrentViewPosition, mCurrentViewPosition);

            // Move Camera to new position
            mCamera.Position = new Point3D(StepSize * mCurrentViewPosition, mCamera.Position.Y, mCamera.Position.Z);

            // Update Info Label
            UpdateInfoLabel();

            // Update Slider
            Slider.Value = mCurrentViewPosition;
        }

        private void SliderDelayTimerEventHandler(Object sender, EventArgs args)
        {
            int SliderValue = Convert.ToInt32(Slider.Value);

            if (SliderValue == mCurrentViewPosition)
                return;

            ShowImage(SliderValue);
        }

        private void AnimationTimerEventHandler(Object sender, EventArgs args)
        {
            mAnimationTimer.Stop();
        }

        private void UserInputBlockTimerEventHandler(Object sender, EventArgs args)
        {
            mUserInputBlockTimer.Stop();
        }

        private void MovieTimerEventHandler(Object sender, EventArgs args)
        {
            if (mCurrentViewPosition == mImageSliceList.Count - 1)
                ShowImage(0);
            else
                ShowImage(mCurrentViewPosition + 1);
        }

        private void SliderDragCompleted(object sender, EventArgs e) 
        {
            mSliderDelayTimer.Stop();
            ShowDefocusedImages();
        } 
        
        private void SliderDragStarted(object sender, EventArgs e) 
        {
            mSliderDelayTimer.Start();
            RemoveDefocusedImages();
        } 

        public void UpdateInfoLabel()
        {
            mInfoLabel.Text = string.Format("Slice {0} of {1} (Z-Value: {2})\n", mCurrentViewPosition + 1, mImageSliceList.Count, mImageSliceList[mCurrentViewPosition].ZValue);

            string[] split = mImageSliceList[mCurrentViewPosition].FileName.Split(new Char[] { '\\' });
            string aFileName = split[split.Length - 1];

            mInfoLabel.Text += string.Format("File: {0}", aFileName);
        }

        public void UserControl_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            if (mUserInputBlockTimer.IsEnabled)
                return;

            switch (e.Key)
            {
                case Key.Right:
                    if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                        ShowImage(mCurrentViewPosition + 1);
                    else
                        MoveToNextImage();
                    break;

                case Key.Left:
                    if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                        ShowImage(mCurrentViewPosition - 1);
                    else
                        MoveToPreviousImage();
                    break;

                case Key.LeftShift:
                case Key.RightShift:
                    RemoveDefocusedImages();
                    break;
            }

            mUserInputBlockTimer.Start();
        }

        public void UserControl_KeyUp(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            switch (e.Key)
            {
                case Key.LeftShift:
                case Key.RightShift:
                    ShowDefocusedImages();
                    break;
            }
        }

        private void UserControl_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;

            if (mUserInputBlockTimer.IsEnabled)
                return;

            if (e.Delta > 0)
            {
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                    ShowImage(mCurrentViewPosition + 1);
                else
                    MoveToNextImage();
            }
            else
            {
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                    ShowImage(mCurrentViewPosition - 1);
                else
                    MoveToPreviousImage();
            }

            mUserInputBlockTimer.Start();
        }
    }
}

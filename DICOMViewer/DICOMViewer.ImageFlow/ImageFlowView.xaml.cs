using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using System.Xml.Linq;

using DICOMViewer.Helper;

// Implementation of the Image Flow View using Windows Presentation Foundation (WPF).
// Major parts of the code have been taken from the 'WPF Cover Flow Tutorial':
// http://d3dal3.blogspot.com/2008/10/wpf-cover-flow-tutorial-part-1.html

namespace DICOMViewer.ImageFlow
{
    public partial class ImageFlowView : Window
    {
        private DispatcherTimer _SliderDelayTimer    = new DispatcherTimer();
        private DispatcherTimer _AnimationTimer      = new DispatcherTimer();
        private DispatcherTimer _UserInputBlockTimer = new DispatcherTimer();
        private int             _CurrentViewPosition = 0;

        private readonly TimeSpan                    _AnimationDuration = TimeSpan.FromMilliseconds(600);
        private readonly int                         _HalfPageSize = 5;
        private readonly double                      _StepSize = 0.1;
        private readonly Dictionary<int, ImageSlice> _ImageSliceList = new Dictionary<int, ImageSlice>();
 
        public ImageFlowView()
        {
            InitializeComponent();

            this.KeyDown += new KeyEventHandler(this.UserControl_KeyDown);
            this.KeyUp += new KeyEventHandler(this.UserControl_KeyUp);
 
            _AnimationTimer.Interval = _AnimationDuration;
            _AnimationTimer.Tick += new EventHandler(AnimationTimerEventHandler);

            _SliderDelayTimer.Interval = TimeSpan.FromMilliseconds(100);
            _SliderDelayTimer.Tick += new EventHandler(SliderDelayTimerEventHandler);

            _UserInputBlockTimer.Interval = TimeSpan.FromMilliseconds(10);
            _UserInputBlockTimer.Tick += new EventHandler(UserInputBlockTimerEventHandler);
        }

        public void MoveToNextImage()
        {
            if (_CurrentViewPosition < _ImageSliceList.Count - 1)
            {
                if (_ImageSliceList.ContainsKey(_CurrentViewPosition - _HalfPageSize))
                    _ImageSliceList[_CurrentViewPosition - _HalfPageSize].ResetBitmap();

                if (_ImageSliceList.ContainsKey(_CurrentViewPosition + _HalfPageSize))
                    _ImageSliceList[_CurrentViewPosition + _HalfPageSize].SetBitmap();

                TransformImageSlice(_CurrentViewPosition, _CurrentViewPosition + _HalfPageSize);

                // Increment current ViewPosition
                _CurrentViewPosition++;

                // ImageSlice, which has the focus, has to be moved to the background
                AnimateImageSlice(_CurrentViewPosition, _CurrentViewPosition - 1);

                // Next ImageSlice has to be brought to the foreground 
                AnimateImageSlice(_CurrentViewPosition, _CurrentViewPosition);

                // Move Camera to new position
                mCamera.Position = new Point3D(_StepSize * _CurrentViewPosition, mCamera.Position.Y, mCamera.Position.Z);

                // Update Info Label
                UpdateInfoLabel();

                // Update Slider
                Slider.Value = _CurrentViewPosition;

                // Start Animation Timer to block further user input until animation is done
                _AnimationTimer.Start();
            }
        }

        public void MoveToPreviousImage()
        {
            if (_CurrentViewPosition > 0)
            {
                if (_ImageSliceList.ContainsKey(_CurrentViewPosition + _HalfPageSize))
                    _ImageSliceList[_CurrentViewPosition + _HalfPageSize].ResetBitmap();

                if (_ImageSliceList.ContainsKey(_CurrentViewPosition - _HalfPageSize))
                    _ImageSliceList[_CurrentViewPosition - _HalfPageSize].SetBitmap();

                TransformImageSlice(_CurrentViewPosition, _CurrentViewPosition - _HalfPageSize);

                // Decrement current ViewPosition
                _CurrentViewPosition--;

                // ImageSlice, which has the focus, has to be moved to the background
                AnimateImageSlice(_CurrentViewPosition, _CurrentViewPosition + 1);

                // Next ImageSlice has to be brought to the foreground 
                AnimateImageSlice(_CurrentViewPosition, _CurrentViewPosition);

                // Move Camera to new position
                mCamera.Position = new Point3D(_StepSize * _CurrentViewPosition, mCamera.Position.Y, mCamera.Position.Z);

                // Update Info Label
                UpdateInfoLabel();

                // Update Slider
                Slider.Value = _CurrentViewPosition;

                // Start Animation Timer to block further user input until animation is done
                _AnimationTimer.Start();
            }
        }

        public void AddImageSlice(CTSliceInfo ct)
        {
            ImageSlice newImageSlice = new ImageSlice(ct, the3DModel);

            // Insert new Image Slice at the end
            _ImageSliceList.Add(_ImageSliceList.Count, newImageSlice);
        }

        public void PostInitialize()
        {
            _CurrentViewPosition = _ImageSliceList.Count / 2;

            // Update min/max value of Slider
            Slider.Minimum = 0;
            Slider.Maximum = _ImageSliceList.Count - 1;

            ShowImage(_CurrentViewPosition);
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

            if (_ImageSliceList.ContainsKey(theSliceIndex))
                _ImageSliceList[theSliceIndex].Animate(aRotationAngle, aTranslationX, aTranslationY, aTranslationZ, _AnimationDuration);
        }

        // Moves slice (with index equal to 'theSliceIndex') to it's new view position
        // Movement is done without animation (transformation only)
        private void TransformImageSlice(int theViewPosition, int theSliceIndex)
        {
            double aRotationAngle = RotationAngle(theViewPosition, theSliceIndex);
            double aTranslationX = TranslationX(theViewPosition, theSliceIndex);
            double aTranslationY = TranslationY(theViewPosition, theSliceIndex);
            double aTranslationZ = TranslationZ(theViewPosition, theSliceIndex);

            if (_ImageSliceList.ContainsKey(theSliceIndex))
                _ImageSliceList[theSliceIndex].Transform(aRotationAngle, aTranslationX, aTranslationY, aTranslationZ);
        }

        private double RotationAngle(int aViewPosition, int aSlicePosition)
        {
            return Math.Sign(aSlicePosition - aViewPosition) * -90;
        }

        private double TranslationX(int aViewPosition, int aSlicePosition)
        {
            return aSlicePosition * _StepSize + Math.Sign(aSlicePosition - aViewPosition) * 3.5;
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
            for (int i = _CurrentViewPosition - _HalfPageSize - 10; i < _CurrentViewPosition + _HalfPageSize + 10; i++)
            {
                if (i != _CurrentViewPosition)
                {
                    if (_ImageSliceList.ContainsKey(i))
                        _ImageSliceList[i].ResetBitmap();
                }
            }
        }

        private void ShowDefocusedImages()
        {
            for (int i = _CurrentViewPosition - _HalfPageSize; i < _CurrentViewPosition + _HalfPageSize; i++)
            {
                if (i != _CurrentViewPosition)
                {
                    if (_ImageSliceList.ContainsKey(i))
                        _ImageSliceList[i].SetBitmap();

                    TransformImageSlice(_CurrentViewPosition, i);
                }
            }
        }

        private void ShowImage(int newPosition)
        {
            if (newPosition < 0 || newPosition > _ImageSliceList.Count - 1)
                return;

            if (_ImageSliceList.ContainsKey(_CurrentViewPosition))
                _ImageSliceList[_CurrentViewPosition].ResetBitmap();

            _CurrentViewPosition = newPosition;

            if (_ImageSliceList.ContainsKey(_CurrentViewPosition))
                _ImageSliceList[_CurrentViewPosition].SetBitmap();

            TransformImageSlice(_CurrentViewPosition, _CurrentViewPosition);

            // Move Camera to new position
            mCamera.Position = new Point3D(_StepSize * _CurrentViewPosition, mCamera.Position.Y, mCamera.Position.Z);

            // Update Info Label
            UpdateInfoLabel();

            // Update Slider
            Slider.Value = _CurrentViewPosition;
        }

        private void SliderDelayTimerEventHandler(Object sender, EventArgs args)
        {
            int SliderValue = Convert.ToInt32(Slider.Value);

            if (SliderValue == _CurrentViewPosition)
                return;

            ShowImage(SliderValue);
        }

        private void AnimationTimerEventHandler(Object sender, EventArgs args)
        {
            _AnimationTimer.Stop();
        }

        private void UserInputBlockTimerEventHandler(Object sender, EventArgs args)
        {
            _UserInputBlockTimer.Stop();
        }

        private void MovieTimerEventHandler(Object sender, EventArgs args)
        {
            if (_CurrentViewPosition == _ImageSliceList.Count - 1)
                ShowImage(0);
            else
                ShowImage(_CurrentViewPosition + 1);
        }

        private void SliderDragCompleted(object sender, EventArgs e) 
        {
            _SliderDelayTimer.Stop();
            ShowDefocusedImages();
        } 
        
        private void SliderDragStarted(object sender, EventArgs e) 
        {
            _SliderDelayTimer.Start();
            RemoveDefocusedImages();
        } 

        public void UpdateInfoLabel()
        {
            mInfoLabel.Text = string.Format("Slice {0} of {1} (Z-Value: {2})\n", _CurrentViewPosition + 1, _ImageSliceList.Count, _ImageSliceList[_CurrentViewPosition].ZValue);

            string[] split = _ImageSliceList[_CurrentViewPosition].FileName.Split(new Char[] { '\\' });
            string aFileName = split[split.Length - 1];

            mInfoLabel.Text += string.Format("File: {0}", aFileName);
        }

        public void UserControl_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            if (_UserInputBlockTimer.IsEnabled)
                return;

            switch (e.Key)
            {
                case Key.Right:
                    if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                        ShowImage(_CurrentViewPosition + 1);
                    else
                        MoveToNextImage();
                    break;

                case Key.Left:
                    if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                        ShowImage(_CurrentViewPosition - 1);
                    else
                        MoveToPreviousImage();
                    break;

                case Key.LeftShift:
                case Key.RightShift:
                    RemoveDefocusedImages();
                    break;
            }

            _UserInputBlockTimer.Start();
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

            if (_UserInputBlockTimer.IsEnabled)
                return;

            if (e.Delta > 0)
            {
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                    ShowImage(_CurrentViewPosition + 1);
                else
                    MoveToNextImage();
            }
            else
            {
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                    ShowImage(_CurrentViewPosition - 1);
                else
                    MoveToPreviousImage();
            }

            _UserInputBlockTimer.Start();
        }
    }
}

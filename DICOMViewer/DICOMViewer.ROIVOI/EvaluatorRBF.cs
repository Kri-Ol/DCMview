using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DICOMViewer.ROIVOI
{
    class EvaluatorRBF : Evaluator
    {
        private float[] _weights = null;

        public EvaluatorRBF(ContourCollection ccol):
            base(ccol)
        {}

        // requires weights recomputation
        protected override void Invalidate()
        {
            _weights = null;
            base.Invalidate();
        }

        public float[] weights
        {
            get { return _weights; }
        }
    }
}

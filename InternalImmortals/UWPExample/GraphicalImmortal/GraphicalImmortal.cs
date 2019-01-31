using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Ambrosia;
using GraphicalImmortalAPI;

namespace GraphicalImmortalImpl
{
    [DataContract]
    public class GraphicalImmortal : Immortal<IGraphicalImmortalProxy>, IGraphicalImmortal
    {
        [DataMember]
        private string _remoteName = string.Empty;
        [DataMember]
        private IGraphicalImmortalProxy _remoteProxy;
        [DataMember]
        private List<Point> _myPoints = new List<Point>();
        [DataMember]
        private List<Point> _otherPoints = new List<Point>();
        [DataMember]
        private bool _prevMyMouseDown = false;
        [DataMember]
        private bool _prevOtherMouseDown = false;

        private bool _doneRecovering = false;

        public GraphicalImmortal(string remoteName)
        {
            _remoteName = remoteName;
        }

        public void HandleUserInputExternal(int x, int y, bool mouseDown)
        {
            if (thisProxy != null)
            {
                thisProxy.AcceptLocalInputFork(x, y, mouseDown);
            }
        }

        public void GetAppStateExternal(out List<Point> myPoints, out List<Point> otherPoints)
        {
            // Create deep copies of the lists instead of returning references
            // to the lists, in order to prevent the client app from modifying
            // zombie state
            lock(_myPoints)
            {
                lock(_otherPoints)
                {
                    myPoints = new List<Point>(_myPoints);
                    otherPoints = new List<Point>(_otherPoints);
                }
            }
        }

        public async Task AcceptLocalInputAsync(int x, int y, bool mouseDown)
        {
            // Update local state corresponding to this client
            lock(_myPoints)
            {
                if (mouseDown && !_prevMyMouseDown)
                {
                    _myPoints.Clear();
                }
                if (mouseDown)
                {
                    _myPoints.Add(new Point(x, y));
                }
            }
            _prevMyMouseDown = mouseDown;

            // Send RPC to update remote state corresponding to this client
            _remoteProxy.AcceptRemoteInputFork(x, y, mouseDown);
        }

        public async Task AcceptRemoteInputAsync(int x, int y, bool mouseDown)
        {
            // Update local state corresponding to remote client
            lock(_otherPoints)
            {
                if (mouseDown && !_prevOtherMouseDown)
                {
                    _otherPoints.Clear();
                }
                if (mouseDown)
                {
                    _otherPoints.Add(new Point(x, y));
                }
            }
            _prevOtherMouseDown = mouseDown;
        }

        protected override async Task<bool> OnFirstStart()
        {
            _remoteProxy = GetProxy<IGraphicalImmortalProxy>(_remoteName);
            return true;
        }

        protected override void BecomingPrimary()
        {
            _doneRecovering = true;
        }

        public bool DoneRecovering()
        {
            return _doneRecovering;
        }
    }
}

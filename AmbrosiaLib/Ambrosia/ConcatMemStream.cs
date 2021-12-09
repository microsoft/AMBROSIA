using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Ambrosia
{
    class ConcatMemStream : Stream
    {
        long _length;
        long _pos;
        List<MemoryStream> _sources;
        bool _canAdd;
        List<MemoryStream>.Enumerator _sourceIterator;

        public ConcatMemStream()
        {
            _length = 0;
            _pos = 0;
            _sources = new List<MemoryStream>();
        }

        public void AddSource(byte [] newSource)
        {
            if (newSource.Length > 0)
            {
                _sources.Add(new MemoryStream(newSource));
            }
            _length += newSource.Length;
        }

        public void AddSource(byte[] newSource,
                              int offset,
                              int length)
        {
            if (length > 0)
            {
                _sources.Add(new MemoryStream(newSource, offset, length));
            }
            _length += length;
        }

        public void AddSource(byte[] newSource,
                              int length)
        {
            if (length > 0)
            {
                _sources.Add(new MemoryStream(newSource, 0, length));
            }
            _length += length;
        }

        public void StartPump()
        {
            if (_pos != 0)
            {
                throw new Exception("Cant start pump after starting stream consumption");
            }
            _sourceIterator = _sources.GetEnumerator();
            _sourceIterator.MoveNext();
        } 

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _length;

        public override long Position { get => _pos; set => throw new NotImplementedException(); }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_pos == _length)
            {
                return 0;
            }

            long bytesLeftInCurrentArray = _sourceIterator.Current.Length - _sourceIterator.Current.Position;
            if (count < bytesLeftInCurrentArray)
            {
                // Get the rest of the bytes from the current array
                _pos += count;
                return _sourceIterator.Current.Read(buffer, offset, count);
            }
            else
            {
                _pos += bytesLeftInCurrentArray;
                _sourceIterator.Current.Read(buffer, offset, (int) bytesLeftInCurrentArray);
                _sourceIterator.MoveNext();
                return (int) bytesLeftInCurrentArray;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}

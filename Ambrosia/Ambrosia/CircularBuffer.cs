// *********************************************************************
//            Copyright (C) Microsoft. All rights reserved.       
// *********************************************************************
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace Ambrosia
{
    [DataContract]
    sealed class CircularBuffer<T>
    {
        public const int DefaultCapacity = 0xfff;
        [DataMember]
        public T[] Items = new T[DefaultCapacity+1];
        [DataMember]
        public int head = 0;
        [DataMember]
        public int tail = 0;

        public CircularBuffer()
        {
        }

        public T PeekFirst()
        {
            return Items[head];
        }

        public T PeekLast()
        {
            return Items[(tail-1) & DefaultCapacity];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(ref T value)
        {
            int next = (tail + 1) & DefaultCapacity;
            if (next == head)
            {
                throw new InvalidOperationException("The list is full!");
            }
            Items[tail] = value;
            tail = next;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Dequeue()
        {
            if (head == tail)
            {
                throw new InvalidOperationException("The list is empty!");
            }
            int oldhead = head;
            head = (head + 1) & DefaultCapacity;
            var ret = Items[oldhead];
            Items[oldhead] = default(T);
            return ret;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFull() => (((tail + 1) & DefaultCapacity) == head);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsEmpty() => (head == tail);

        public IEnumerable<T> Iterate()
        {
            int i = head;
            while (i != tail)
            {
                yield return Items[i];
                i = (i + 1) & DefaultCapacity;
            }
        }
    }

    /// <summary>
    /// Currently for internal use only - do not use directly.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [DataContract]
    public sealed class ElasticCircularBuffer<T> : IEnumerable<T>
    {
        private LinkedList<CircularBuffer<T>> buffers;
        private LinkedListNode<CircularBuffer<T>> head;
        private LinkedListNode<CircularBuffer<T>> tail;
        private int count;

        /// <summary>
        /// Currently for internal use only - do not use directly.
        /// </summary>
        public ElasticCircularBuffer()
        {
            buffers = new LinkedList<CircularBuffer<T>>();
            var node = new LinkedListNode<CircularBuffer<T>>(new CircularBuffer<T>());
            buffers.AddFirst(node);
            tail = head = node;
            count = 0;
        }

        /// <summary>
        /// Currently for internal use only - do not use directly.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(ref T value)
        {
            if (tail.Value.IsFull())
            {
                var next = tail.Next;
                if (next == null) next = buffers.First;
                if (!next.Value.IsEmpty())
                {
                    next = new LinkedListNode<CircularBuffer<T>>(new CircularBuffer<T>());
                    buffers.AddAfter(tail, next);
                }
                tail = next;
            }
            tail.Value.Enqueue(ref value);
            count++;
        }

        /// <summary>
        /// Currently for internal use only - do not use directly.
        /// </summary>
        /// <param name="value"></param>
        public void Add(T value)
        {
            Enqueue(ref value);
        }

        /// <summary>
        /// Currently for internal use only - do not use directly.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Dequeue()
        {
            if (head.Value.IsEmpty())
            {
                if (head == tail)
                    throw new InvalidOperationException("The list is empty!");

                head = head.Next;
                if (head == null) head = buffers.First;
            }
            count--;
            return head.Value.Dequeue();
        }

        /// <summary>
        /// Currently for internal use only - do not use directly.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T PeekFirst()
        {
            //if (head.Value.IsEmpty())
            if (head.Value.head == head.Value.tail)
            {
                if (head == tail)
                    throw new InvalidOperationException("The list is empty!");

                head = head.Next;
                if (head == null) head = buffers.First;
            }
            //return head.Value.PeekFirst();
            return head.Value.Items[head.Value.head];
        }

        /// <summary>
        /// Currently for internal use only - do not use directly.
        /// </summary>
        /// <returns></returns>
        public T PeekLast()
        {
            if (tail.Value.IsEmpty())
                throw new InvalidOperationException("The list is empty!");
            return tail.Value.PeekLast();
        }

        /// <summary>
        /// Currently for internal use only - do not use directly.
        /// </summary>
        /// <returns></returns>
        public bool IsEmpty() => (head.Value.IsEmpty() && (head == tail));

        IEnumerable<T> Iterate()
        {
            foreach (CircularBuffer<T> buffer in buffers)
            {
                foreach (T item in buffer.Iterate())
                {
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Currently for internal use only - do not use directly.
        /// </summary>
        public int Count => count;

        /// <summary>
        /// Currently for internal use only - do not use directly.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<T> GetEnumerator() => Iterate().GetEnumerator();

        IEnumerable<T> LastItemEnumerable()
        {
            if (tail.Value.IsEmpty())
                throw new InvalidOperationException("The list is empty!");
            yield return PeekLast();
        }

        // Wrap the last item in an enumerator
        public IEnumerator<T> GetLastEnumerator()
        {
            var enumerator = LastItemEnumerable().GetEnumerator();
            enumerator.MoveNext();
            return enumerator;
        }
        

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

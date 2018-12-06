using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Ambrosia
{
    [DataContract]
    public class SerializableTaskCompletionSource
    {
        [DataMember] public bool IsCompleted { get; set; }

        [DataMember] public ResultAdditionalInfo ResultAdditionalInfo { get; set; }

        [DataMember] public long SequenceNumber { get; set; }

        [DataMember] public SerializableType ResultType { get; set; }

        [DataMember] public SerializableException SerializableException { get; set; }

        private object _taskCompletionSource;
        private TaskCompletionSource<ResultAdditionalInfo> _taskCompletionSourceAdditionalInfo;
        private object _taskCompletionSourceLocker;

        public SerializableTaskCompletionSource()
        {
            this.SerializableException = null;

            this._taskCompletionSourceLocker = new object();
            this._taskCompletionSourceAdditionalInfo = new TaskCompletionSource<ResultAdditionalInfo>();
        }

        public SerializableTaskCompletionSource(Type resultType, long sequenceNumber = -1) : this()
        {
            this.ResultType = new SerializableType(resultType);
            this.ResultAdditionalInfo = new ResultAdditionalInfo(null, resultType);
            this.SequenceNumber = sequenceNumber;

            var genericTaskCompletionSourceType = typeof(TaskCompletionSource<>);
            var taskCompletionSourceType = genericTaskCompletionSourceType.MakeGenericType(resultType);
            this._taskCompletionSource = Activator.CreateInstance(taskCompletionSourceType);
        }

        public void SetException(SerializableException ex)
        {
            var setException = this.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.GetCustomAttributes(typeof(SetExceptionAttribute)).Any());

            if (setException != null)
            {
                var genericSetExceptionMethod = setException.MakeGenericMethod(this.ResultType.Type);
                genericSetExceptionMethod.Invoke(this, new object[] { ex });
            }
        }

        [SetException]
        private void SetException<T>(SerializableException ex)
        {
            this._taskCompletionSourceAdditionalInfo.SetException(ex.Exception);
            ((TaskCompletionSource<T>)this._taskCompletionSource).SetException(ex.Exception);

            this.SerializableException = ex;
            this.IsCompleted = true;
        }

        public void SetResult(object result, ResultAdditionalInfoTypes resultAdditionalInfo = ResultAdditionalInfoTypes.SetResult)
        {
            if (result != null && result.GetType() != this.ResultType.Type)
            {
                throw new ArgumentException(
                    $"SetResult called with type: {result.GetType().FullName}, but expected type is: {this.ResultType.Type.FullName}.");
            }

            var setResult = this.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.GetCustomAttributes(typeof(SetResultAttribute)).Any());
            if (setResult != null)
            {
                var genericSetResultMethod = setResult.MakeGenericMethod(this.ResultType.Type);
                genericSetResultMethod.Invoke(this, new[] {result, resultAdditionalInfo});
            }
        }

        [SetResult]
        private void SetResult<T>(T result, ResultAdditionalInfoTypes resultAdditionalInfoType = ResultAdditionalInfoTypes.SetResult)
        {
            if (result != null && result.GetType() != this.ResultType.Type)
            {
                throw new ArgumentException(
                    $"SetResult called with type: {result.GetType().FullName}, but expected type is: {this.ResultType.Type.FullName}.");
            }

            lock (this._taskCompletionSourceLocker)
            {
                this.ResultAdditionalInfo = new ResultAdditionalInfo(result, this.ResultType.Type, resultAdditionalInfoType);
                this._taskCompletionSourceAdditionalInfo.SetResult(this.ResultAdditionalInfo);
                ((TaskCompletionSource<T>) this._taskCompletionSource).SetResult(result);
                this.IsCompleted = true;
            }
        }

        [SetTakeCheckpoint]
        public void SetTakeCheckpoint<T>()
        {
            this.SetResult(default(T), ResultAdditionalInfoTypes.TakeCheckpoint);
        }

        [SetSaveContext]
        public void SetSaveContext<T>()
        {
            this.SetResult(default(T), ResultAdditionalInfoTypes.SaveContext);
        }

        [GetAwaitableTask]
        public Task<T> GetAwaitableTaskAsync<T>()
        {
            lock (this._taskCompletionSourceLocker)
            {
                return ((TaskCompletionSource<T>) this._taskCompletionSource).Task;
            }
        }

        public Task<ResultAdditionalInfo> GetAwaitableTaskWithAdditionalInfoAsync()
        {
            lock (this._taskCompletionSourceLocker)
            {
                return this._taskCompletionSourceAdditionalInfo.Task;
            }
        }

        [AttributeUsage(AttributeTargets.Method)]
        public class SetResultAttribute : Attribute
        {

        }

        [AttributeUsage(AttributeTargets.Method)]
        public class SetExceptionAttribute : Attribute
        {

        }

        [AttributeUsage(AttributeTargets.Method)]
        public class GetAwaitableTaskAttribute : Attribute
        {

        }

        [AttributeUsage(AttributeTargets.Method)]
        public class SetTakeCheckpointAttribute : Attribute
        {

        }

        [AttributeUsage(AttributeTargets.Method)]
        public class SetSaveContextAttribute : Attribute
        {

        }

        [OnDeserialized]
        public void SetTaskCompletionSource(StreamingContext context)
        {
            this._taskCompletionSourceLocker = new object();

            lock (this._taskCompletionSourceLocker)
            {
                var genericTaskCompletionSourceType = typeof(TaskCompletionSource<>);
                var taskCompletionSourceType = genericTaskCompletionSourceType.MakeGenericType(this.ResultType.Type);
                this._taskCompletionSource = Activator.CreateInstance(taskCompletionSourceType);
                this._taskCompletionSourceAdditionalInfo = new TaskCompletionSource<ResultAdditionalInfo>();
            }

            if (this.IsCompleted)
            {
                if (this.SerializableException != null)
                {
                    this.SetException(this.SerializableException);
                }
                else
                {
                    this.SetResult(this.ResultAdditionalInfo.Result);
                }
            }
        }

        public SerializableTaskCompletionSourceAwaiter GetAwaiter()
        {
            return new SerializableTaskCompletionSourceAwaiter { Task = this };
        }
    }

    [DataContract]
    public class SerializableTaskCompletionSourceAwaiter : INotifyCompletion
    {
        [DataMember]
        public SerializableTaskCompletionSource Task;

        public bool IsCompleted => this.Task.IsCompleted;

        public object GetResult()
        {
            return this.Task.ResultAdditionalInfo.Result;
        }

        public void OnCompleted(Action continuation)
        {
            continuation();
        }
    }

    [DataContract]
    public class ResultAdditionalInfo
    {
        [DataMember] public ResultAdditionalInfoTypes AdditionalInfoType { get; set; }

        [DataMember] public string SerializedResult { get; set; }

        [DataMember] public SerializableType ResultType { get; set; }

        public object Result { get; set; }

        public ResultAdditionalInfo()
        {
            
        }

        public ResultAdditionalInfo(object result, Type resultType, ResultAdditionalInfoTypes additionalInfoType = ResultAdditionalInfoTypes.SetResult)
        {
            this.Result = result;
            this.ResultType = new SerializableType(resultType);
            this.AdditionalInfoType = additionalInfoType;
        }

        [OnSerializing]
        public void SetSerializedResult(StreamingContext context)
        {
            this.SerializedResult = this.Result == null ? string.Empty : JsonConvert.SerializeObject(this.Result);
        }

        [OnDeserialized]
        public void SetResult(StreamingContext context)
        {
            if (this.SerializedResult != string.Empty)
            {
                var resultObject = JsonConvert.DeserializeObject(this.SerializedResult, this.ResultType.Type);
                this.Result = Convert.ChangeType(resultObject, this.ResultType.Type);
            }
        }
    }
    public enum ResultAdditionalInfoTypes
    {
        SetResult,
        TakeCheckpoint,
        SaveContext,
    }
}
﻿using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Grpc.Core;
using NAPS2.Scan.Exceptions;
using NAPS2.Util;

namespace NAPS2.Remoting
{
    public static class RemotingHelper
    {
        public static void HandleErrors(Error error)
        {
            if (!string.IsNullOrEmpty(error?.Type))
            {
                var exceptionType = Assembly.GetAssembly(typeof(ScanDriverException))
                    .GetTypes()
                    .FirstOrDefault(x => x.FullName == error.Type);
                if (exceptionType != null)
                {
                    var exception = (Exception)Activator.CreateInstance(exceptionType);
                    var messageField = typeof(Exception).GetField("_message", BindingFlags.NonPublic | BindingFlags.Instance);
                    var stackTraceField = typeof(Exception).GetField("_stackTraceString", BindingFlags.NonPublic | BindingFlags.Instance);
                    messageField?.SetValue(exception, error.Message);
                    stackTraceField?.SetValue(exception, error.StackTrace);
                    exception.PreserveStackTrace();
                    throw exception;
                }
                throw new Exception($"An error occurred on the gRPC server.\n{error.Type}: {error.Message}\n{error.StackTrace}");
            }
        }

        private static Error ToError(Exception e) =>
            new Error
            {
                Type = e.GetType().FullName,
                Message = e.Message,
                StackTrace = e.StackTrace
            };

        public static Task<TResponse> WrapFunc<TResponse>(Func<TResponse> action, Func<Error, TResponse> error) =>
            Task.Run(() =>
            {
                try
                {
                    return action();
                }
                catch (Exception e)
                {
                    return error(ToError(e));
                }
            });

        public static Task<TResponse> WrapFunc<TResponse>(Func<Task<TResponse>> action, Func<Error, TResponse> error) =>
            Task.Run(async () =>
            {
                try
                {
                    return await action();
                }
                catch (Exception e)
                {
                    return error(ToError(e));
                }
            });

        public static Task WrapAction(Func<Task> action, Action<Error> error) =>
            Task.Run(async () =>
            {
                try
                {
                    await action();
                }
                catch (Exception e)
                {
                    error(ToError(e));
                }
            });

        public static Task WrapAction(Action action, Action<Error> error) =>
            Task.Run(() =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    error(ToError(e));
                }
            });
    }
}

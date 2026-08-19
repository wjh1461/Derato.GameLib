using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Derato.GameLib.Utilities
{
    public static class AwaitableExtensions
    {
        public static async void Forget(this Awaitable task, [CallerMemberName] string callerName = "") 
        {
			try
			{
                await task;
            }
			catch (OperationCanceledException)
			{
				Debug.LogWarning($"[{callerName}] 비동기 작업이 취소됨.");                
			}
            catch (Exception ex)
            {
                Debug.LogError($"[{callerName}] Awaitable Fire-and-Forget 실행 중 에러 발생: {ex.Message}");
                Debug.LogException(ex);
            }
        }
    }
}

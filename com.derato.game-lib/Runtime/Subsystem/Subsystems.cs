using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Derato.GameLib
{
    public interface ISubsystem
    {
        public void Initialize();
    }

    public interface IAsyncInitialize
    {
        public Awaitable InitializeAsync(CancellationToken ct);
    }

    /// <summary>
    /// 전역 접근 가능
    /// </summary>
    public static class SubsystemLocator
    {
        private static readonly Dictionary<Type, ISubsystem> subsystems = new();

        public static void RegisterSubsystem<T>(T subsystem) where T : ISubsystem
        {
            Type type = typeof(T);
            if (subsystems.ContainsKey(type))
            {
                Debug.LogWarning($"Subsystem of type {type} is already registered. Overwriting.");
            }
            subsystems[type] = subsystem;
        }

        public static void UnregisterSubsystem<T>(T subsystem) where T : ISubsystem
        {
            Type type = typeof(T);
            if (subsystems.ContainsKey(type) && ReferenceEquals(subsystems[type], subsystem))
            {
                subsystems.Remove(type);
            }
            else
            {
                Debug.LogWarning($"Subsystem of type {type} is not registered or does not match the provided instance.");
            }
        }

        public static T GetSubsystem<T>() where T : ISubsystem
        {
            Type type = typeof(T);
            if (subsystems.TryGetValue(type, out ISubsystem subsystem))
            {
                return (T)subsystem;
            }
            throw new Exception($"Subsystem of type {type} is not registered.");
        }

        // 등록된 서브시스템 중 비동기 초기화가 필요한 것들만 일괄 대기하는 함수
        public static async Awaitable InitializeAllAsync(CancellationToken ct)
        {
            foreach (var subsystem in subsystems.Values)
            {
                if (subsystem is IAsyncInitialize asyncSubsystem)
                {
                    await asyncSubsystem.InitializeAsync(ct);
                }
            }
        }
    }
}

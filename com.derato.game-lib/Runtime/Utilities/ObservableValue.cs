using System;
using System.Collections.Generic;

namespace Derato.GameLib.Utilities
{
    /// <summary>
    /// 반응형 UI 구현을 위한 관찰 가능한 값 래퍼 클래스
    /// </summary>    
    public sealed class ObservableValue<T>
    {
        private T value;
        private event Action<T> OnChanged;

        public T Value
        {
            get => value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(this.value, value))
                {
                    return;
                }

                this.value = value;
                OnChanged?.Invoke(this.value);
            }
        }

        public IDisposable Subscribe(Action<T> listener)
        {
            OnChanged += listener;
            listener?.Invoke(value); // 초기값 즉시 전달

            return new Subscription(() => OnChanged -= listener);
        }

        private sealed class Subscription : IDisposable
        {
            private Action dispose;

            public Subscription(Action dispose) => this.dispose = dispose;

            public void Dispose()
            {
                dispose?.Invoke();
                dispose = null;
            }
        }
    }

    public interface IObservableBinding<T>
    {
        public void Bind(T value);
    }
}

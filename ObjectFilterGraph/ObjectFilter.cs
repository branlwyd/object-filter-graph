using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ObjectFilterGraph
{
    public class PinIn<T>
    {
        IObjectFilter<T> owner;

        public PinIn(IObjectFilter<T> owner)
        {
            this.owner = owner;
        }

        /* 
         * note: Receive must take an object instead of a T in order to be called asynchronously
         */
        internal void Receive(object item)
        {
            T itm = (T)item;
            owner.Receive(this, itm);
        }
    }

    public class PinOut<T>
    {
        List<PinIn<T>> attached;

        public PinOut()
        {
            this.attached = new List<PinIn<T>>();
        }

        public void AttachTo(PinIn<T> pin)
        {
            if (!attached.Contains(pin))
                attached.Add(pin);
        }

        public void Send(T item)
        {
            if (item is ICloneable)
            {
                List<PinIn<T>>.Enumerator pins = attached.GetEnumerator();
                PinIn<T> firstPin;

                if (!pins.MoveNext()) return;
                firstPin = pins.Current;
                while(pins.MoveNext())
                    ThreadPool.QueueUserWorkItem(pins.Current.Receive, ((ICloneable)item).Clone());
                ThreadPool.QueueUserWorkItem(firstPin.Receive, item);
            }
            else
            {
                /* 
                 * note: assumption is that if item is not cloneable, it is immutable.
                 * do not break this assumption or extremely weird behavior can and likely will occur.
                 */
                foreach (PinIn<T> pin in attached)
                    ThreadPool.QueueUserWorkItem(pin.Receive, item);
            }
        }
    }

    public interface IObjectFilter<T>
    {
        void Receive(PinIn<T> pin, T item);
    }

    public abstract class ObjectSource<T> : IObjectFilter<T>
    {
        public PinIn<T> InputPin;
        public PinOut<T> OutputPin;

        public abstract T Generate();

        public ObjectSource()
        {
            InputPin = new PinIn<T>(this);
            OutputPin = new PinOut<T>();
        }

        public void Receive(PinIn<T> pin, T item)
        {
            if (item is IDisposable)
                ((IDisposable)item).Dispose();

            OutputPin.Send(Generate());
        }
    }
}

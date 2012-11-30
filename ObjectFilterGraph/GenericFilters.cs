using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ObjectFilterGraph.GenericFilters
{
    public class GenericDelay<T> : IObjectFilter<T>
    {
        public PinIn<T> InputPin;
        public PinOut<T> OutputPin;

        private TimeSpan interval;
        private DateTime next;

        public GenericDelay(TimeSpan interval)
        {
            InputPin = new PinIn<T>(this);
            OutputPin = new PinOut<T>();

            this.interval = interval;
            next = DateTime.Now;
        }

        public void Receive(PinIn<T> pin, T item)
        {
            TimeSpan delay;

            lock (this)
            {
                delay = next - DateTime.Now;
                if(next < DateTime.Now)
                    next = DateTime.Now + interval;
                else
                    next = next + interval;
            }

            if (delay.TotalSeconds > 0)
                Thread.Sleep(delay);
            OutputPin.Send(item);
        }
    }

    public class GenericTransform<T> : IObjectFilter<T>
    {
        public PinIn<T> InputPin;
        public PinOut<T> OutputPin;

        private Func<T, T> transformFunction;

        public GenericTransform(Func<T, T> transformFunction)
        {
            this.transformFunction = transformFunction;
            this.InputPin = new PinIn<T>(this);
            this.OutputPin = new PinOut<T>();
        }

        public void Receive(PinIn<T> pin, T item)
        {
            OutputPin.Send(transformFunction(item));
        }
    }

    public class GenericFilter<T> : IObjectFilter<T>
    {
        public PinIn<T> InputPin;
        public PinOut<T> SuccessPin;
        public PinOut<T> FailurePin;

        private Predicate<T> filterFunction;

        public GenericFilter(Predicate<T> filterFunction)
        {
            this.filterFunction = filterFunction;
            this.InputPin = new PinIn<T>(this);
            this.SuccessPin = new PinOut<T>();
            this.FailurePin = new PinOut<T>();
        }

        public void Receive(PinIn<T> pin, T item)
        {
            PinOut<T> outPin = filterFunction(item) ? SuccessPin : FailurePin;
            outPin.Send(item);
        }
    }
}
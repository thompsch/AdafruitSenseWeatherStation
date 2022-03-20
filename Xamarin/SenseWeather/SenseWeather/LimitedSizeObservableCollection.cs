using System;
using System.Collections.ObjectModel;

namespace SenseWeather
{
    public class LimitedSizeObservableCollection<T> : ObservableCollection<T>
    {
        public int Capacity { get; }

        public LimitedSizeObservableCollection(int capacity)
        {
            Capacity = capacity;
        }

        protected override void InsertItem(int index, T item)
        {
            if (Count >= Capacity)
            {
                this.RemoveAt(0);
                index -= 1;
            }
            base.InsertItem(index, item);
        }
    }
}

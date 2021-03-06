﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Npgsql;
#pragma warning disable 1591

// ReSharper disable once CheckNamespace
namespace NpgsqlTypes
{
    /// <summary>
    /// A struct similar to DateTime but capable of storing PostgreSQL's timestamp and timestamptz types. DateTime
    /// is capable of storing values from year 1 to 9999 at 100-nanosecond precision, while PostgreSQL's timestamps
    /// store values from 4713BC to 5874897AD with 1-microsecond precision.
    /// </summary>
#if !DNXCORE50
    [Serializable]
#endif
    public struct NpgsqlDateTime : IEquatable<NpgsqlDateTime>, IComparable<NpgsqlDateTime>, IComparable,
                                    IComparer<NpgsqlDateTime>, IComparer
    {
        #region Fields

        readonly NpgsqlDate _date;
        readonly TimeSpan _time;
        readonly InternalType _type;

        #endregion

        #region Constants

        public static readonly NpgsqlDateTime Epoch = new NpgsqlDateTime(NpgsqlDate.Epoch);
        public static readonly NpgsqlDateTime Era = new NpgsqlDateTime(NpgsqlDate.Era);

        public static readonly NpgsqlDateTime Infinity =
            new NpgsqlDateTime(InternalType.Infinity, NpgsqlDate.Era, TimeSpan.Zero);

        public static readonly NpgsqlDateTime NegativeInfinity =
            new NpgsqlDateTime(InternalType.NegativeInfinity, NpgsqlDate.Era, TimeSpan.Zero);

        #endregion

        #region Constructors

        NpgsqlDateTime(InternalType type, NpgsqlDate date, TimeSpan time)
        {
            _type = type;
            _date = date;
            _time = time;
        }

        public NpgsqlDateTime(NpgsqlDate date, TimeSpan time, DateTimeKind kind = DateTimeKind.Unspecified)
            : this(KindToInternalType(kind), date, time) {}

        public NpgsqlDateTime(NpgsqlDate date)
            : this(date, TimeSpan.Zero) {}

        public NpgsqlDateTime(int year, int month, int day, int hours, int minutes, int seconds, DateTimeKind kind=DateTimeKind.Unspecified)
            : this(new NpgsqlDate(year, month, day), new TimeSpan(0, hours, minutes, seconds), kind) {}

        public NpgsqlDateTime(int year, int month, int day, int hours, int minutes, int seconds, int milliseconds, DateTimeKind kind = DateTimeKind.Unspecified)
            : this(new NpgsqlDate(year, month, day), new TimeSpan(0, hours, minutes, seconds, milliseconds), kind) { }

        public NpgsqlDateTime(DateTime dateTime)
            : this(new NpgsqlDate(dateTime.Date), dateTime.TimeOfDay, dateTime.Kind) {}

        public NpgsqlDateTime(long ticks, DateTimeKind kind)
            : this(new DateTime(ticks, kind)) { }

        public NpgsqlDateTime(long ticks)
            : this(new DateTime(ticks, DateTimeKind.Unspecified)) { }

        #endregion

        #region Public Properties

        public NpgsqlDate Date { get { return _date; } }
        public TimeSpan Time { get { return _time; } }
        public int DayOfYear { get { return _date.DayOfYear; } }
        public int Year { get { return _date.Year; } }
        public int Month { get { return _date.Month; } }
        public int Day { get { return _date.Day; } }
        public DayOfWeek DayOfWeek { get { return _date.DayOfWeek; } }
        public bool IsLeapYear { get { return _date.IsLeapYear; } }

        public long Ticks { get { return _date.DaysSinceEra * NpgsqlTimeSpan.TicksPerDay + _time.Ticks; } }
        public int Milliseconds { get { return _time.Milliseconds; } }
        public int Seconds { get { return _time.Seconds; } }
        public int Minutes { get { return _time.Minutes; } }
        public int Hours { get { return _time.Hours; } }
        public bool IsInfinity { get { return _type == InternalType.Infinity; } }
        public bool IsNegativeInfinity { get { return _type == InternalType.NegativeInfinity; } }

        public bool IsFinite
        {
            get
            {
                switch (_type) {
                case InternalType.FiniteUnspecified:
                case InternalType.FiniteUtc:
                case InternalType.FiniteLocal:
                    return true;
                case InternalType.Infinity:
                case InternalType.NegativeInfinity:
                    return false;
                default:
                    throw PGUtil.ThrowIfReached();
                }
            }
        }

        public DateTimeKind Kind
        {
            get
            {
                switch (_type)
                {
                case InternalType.FiniteUtc:
                    return DateTimeKind.Utc;
                case InternalType.FiniteLocal:
                    return DateTimeKind.Local;
                case InternalType.FiniteUnspecified:
                case InternalType.Infinity:
                case InternalType.NegativeInfinity:
                    return DateTimeKind.Unspecified;
                default:
                    throw new ArgumentOutOfRangeException();
                }
            }
        }

        public DateTime DateTime
        {
            get
            {
                if (!IsFinite)
                    throw new InvalidCastException("Can't convert infinite timestamp values to DateTime");
                if (Year < 1 || Year > 9999)
                    throw new InvalidCastException("Out of the range of DateTime (year must be between 1 and 9999)");
                Contract.EndContractBlock();

                return new DateTime(Year, Month, Day, 0, 0, 0, Kind) + Time;
            }
        }

        public NpgsqlDateTime ToUniversalTime()
        {
            switch (_type)
            {
            case InternalType.FiniteUnspecified:
                // Treat as Local
            case InternalType.FiniteLocal:
                return new NpgsqlDateTime(Subtract(TimeZoneInfo.Local.BaseUtcOffset).Ticks, DateTimeKind.Utc);
            case InternalType.FiniteUtc:
            case InternalType.Infinity:
            case InternalType.NegativeInfinity:
                return this;
            default:
                throw PGUtil.ThrowIfReached();
            }
        }

        public NpgsqlDateTime ToLocalTime()
        {
            switch (_type) {
            case InternalType.FiniteUnspecified:
                // Treat as UTC
            case InternalType.FiniteUtc:
                return new NpgsqlDateTime(Add(TimeZoneInfo.Local.BaseUtcOffset).Ticks, DateTimeKind.Local);
            case InternalType.FiniteLocal:
            case InternalType.Infinity:
            case InternalType.NegativeInfinity:
                return this;
            default:
                throw PGUtil.ThrowIfReached();
            }
        }

        public static NpgsqlDateTime Now { get { return new NpgsqlDateTime(DateTime.Now); } }

        #endregion

        #region String Conversions

        public override string ToString()
        {
            switch (_type) {
            case InternalType.Infinity:
                return "infinity";
            case InternalType.NegativeInfinity:
                return "-infinity";
            default:
                return string.Format("{0} {1}", _date, _time);
            }
        }

        public static NpgsqlDateTime Parse(string str)
        {
            if (str == null) {
                throw new NullReferenceException();
            }
            switch (str = str.Trim().ToLowerInvariant()) {
            case "infinity":
                return Infinity;
            case "-infinity":
                return NegativeInfinity;
            default:
                try {
                    int idxSpace = str.IndexOf(' ');
                    string datePart = str.Substring(0, idxSpace);
                    if (str.Contains("bc")) {
                        datePart += " BC";
                    }
                    int idxSecond = str.IndexOf(' ', idxSpace + 1);
                    if (idxSecond == -1) {
                        idxSecond = str.Length;
                    }
                    string timePart = str.Substring(idxSpace + 1, idxSecond - idxSpace - 1);
                    return new NpgsqlDateTime(NpgsqlDate.Parse(datePart), TimeSpan.Parse(timePart));
                } catch (OverflowException) {
                    throw;
                } catch {
                    throw new FormatException();
                }
            }
        }

        #endregion

        #region Comparisons

        public bool Equals(NpgsqlDateTime other)
        {
            switch (_type) {
            case InternalType.Infinity:
                return other._type == InternalType.Infinity;
            case InternalType.NegativeInfinity:
                return other._type == InternalType.NegativeInfinity;
            default:
                return other._type == _type && _date.Equals(other._date) && _time.Equals(other._time);
            }
        }

        public override bool Equals(object obj)
        {
            return obj is NpgsqlDateTime && Equals((NpgsqlDateTime)obj);
        }

        public override int GetHashCode()
        {
            switch (_type) {
            case InternalType.Infinity:
                return int.MaxValue;
            case InternalType.NegativeInfinity:
                return int.MinValue;
            default:
                return _date.GetHashCode() ^ PGUtil.RotateShift(_time.GetHashCode(), 16);
            }
        }

        public int CompareTo(NpgsqlDateTime other)
        {
            switch (_type) {
            case InternalType.Infinity:
                return other._type == InternalType.Infinity ? 0 : 1;
            case InternalType.NegativeInfinity:
                return other._type == InternalType.NegativeInfinity ? 0 : -1;
            default:
                switch (other._type) {
                case InternalType.Infinity:
                    return -1;
                case InternalType.NegativeInfinity:
                    return 1;
                default:
                    int cmp = _date.CompareTo(other._date);
                    return cmp == 0 ? _time.CompareTo(_time) : cmp;
                }
            }
        }

        public int CompareTo(object obj)
        {
            if (obj == null) {
                return 1;
            }
            if (obj is NpgsqlDateTime) {
                return CompareTo((NpgsqlDateTime)obj);
            }
            throw new ArgumentException();
        }

        public int Compare(NpgsqlDateTime x, NpgsqlDateTime y)
        {
            return x.CompareTo(y);
        }

        public int Compare(object x, object y)
        {
            if (x == null) {
                return y == null ? 0 : -1;
            }
            if (y == null) {
                return 1;
            }
            if (!(x is IComparable) || !(y is IComparable)) {
                throw new ArgumentException();
            }
            return ((IComparable)x).CompareTo(y);
        }

        #endregion

        #region Arithmetic

        /// <summary>
        /// Returns a new <see cref="NpgsqlDateTime"/> that adds the value of the specified TimeSpan to the value of this instance.
        /// </summary>
        /// <param name="value">A positive or negative time interval.</param>
        /// <returns>An object whose value is the sum of the date and time represented by this instance and the time interval represented by value.</returns>
        [Pure]
        public NpgsqlDateTime Add(NpgsqlTimeSpan value) { return AddTicks(value.Ticks); }

        /// <summary>
        /// Returns a new <see cref="NpgsqlDateTime"/> that adds the value of the specified <see cref="NpgsqlTimeSpan"/> to the value of this instance.
        /// </summary>
        /// <param name="value">A positive or negative time interval.</param>
        /// <returns>An object whose value is the sum of the date and time represented by this instance and the time interval represented by value.</returns>
        [Pure]
        public NpgsqlDateTime Add(TimeSpan value) { return AddTicks(value.Ticks); }

        /// <summary>
        /// Returns a new <see cref="NpgsqlDateTime"/> that adds the specified number of years to the value of this instance.
        /// </summary>
        /// <param name="value">A number of years. The value parameter can be negative or positive.</param>
        /// <returns>An object whose value is the sum of the date and time represented by this instance and the number of years represented by value.</returns>
        [Pure]
        public NpgsqlDateTime AddYears(int value)
        {
            switch (_type) {
            case InternalType.Infinity:
            case InternalType.NegativeInfinity:
                return this;
            default:
                return new NpgsqlDateTime(_type, _date.AddYears(value), _time);
            }
        }

        /// <summary>
        /// Returns a new <see cref="NpgsqlDateTime"/> that adds the specified number of months to the value of this instance.
        /// </summary>
        /// <param name="value">A number of months. The months parameter can be negative or positive.</param>
        /// <returns>An object whose value is the sum of the date and time represented by this instance and months.</returns>
        [Pure]
        public NpgsqlDateTime AddMonths(int value)
        {
            switch (_type) {
            case InternalType.Infinity:
            case InternalType.NegativeInfinity:
                return this;
            default:
                return new NpgsqlDateTime(_type, _date.AddMonths(value), _time);
            }
        }

        /// <summary>
        /// Returns a new <see cref="NpgsqlDateTime"/> that adds the specified number of days to the value of this instance.
        /// </summary>
        /// <param name="value">A number of whole and fractional days. The value parameter can be negative or positive.</param>
        /// <returns>An object whose value is the sum of the date and time represented by this instance and the number of days represented by value.</returns>
        [Pure]
        public NpgsqlDateTime AddDays(double value) { return Add(TimeSpan.FromDays(value)); }

        /// <summary>
        /// Returns a new <see cref="NpgsqlDateTime"/> that adds the specified number of hours to the value of this instance.
        /// </summary>
        /// <param name="value">A number of whole and fractional hours. The value parameter can be negative or positive.</param>
        /// <returns>An object whose value is the sum of the date and time represented by this instance and the number of hours represented by value.</returns>
        [Pure]
        public NpgsqlDateTime AddHours(double value) { return Add(TimeSpan.FromHours(value)); }

        /// <summary>
        /// Returns a new <see cref="NpgsqlDateTime"/> that adds the specified number of minutes to the value of this instance.
        /// </summary>
        /// <param name="value">A number of whole and fractional minutes. The value parameter can be negative or positive.</param>
        /// <returns>An object whose value is the sum of the date and time represented by this instance and the number of minutes represented by value.</returns>
        [Pure]
        public NpgsqlDateTime AddMinutes(double value) { return Add(TimeSpan.FromMinutes(value)); }

        /// <summary>
        /// Returns a new <see cref="NpgsqlDateTime"/> that adds the specified number of minutes to the value of this instance.
        /// </summary>
        /// <param name="value">A number of whole and fractional minutes. The value parameter can be negative or positive.</param>
        /// <returns>An object whose value is the sum of the date and time represented by this instance and the number of minutes represented by value.</returns>
        [Pure]
        public NpgsqlDateTime AddSeconds(double value) { return Add(TimeSpan.FromSeconds(value)); }

        /// <summary>
        /// Returns a new <see cref="NpgsqlDateTime"/> that adds the specified number of milliseconds to the value of this instance.
        /// </summary>
        /// <param name="value">A number of whole and fractional milliseconds. The value parameter can be negative or positive. Note that this value is rounded to the nearest integer.</param>
        /// <returns>An object whose value is the sum of the date and time represented by this instance and the number of milliseconds represented by value.</returns>
        [Pure]
        public NpgsqlDateTime AddMilliseconds(double value) { return Add(TimeSpan.FromMilliseconds(value)); }

        /// <summary>
        /// Returns a new <see cref="NpgsqlDateTime"/> that adds the specified number of ticks to the value of this instance.
        /// </summary>
        /// <param name="value">A number of 100-nanosecond ticks. The value parameter can be positive or negative.</param>
        /// <returns>An object whose value is the sum of the date and time represented by this instance and the time represented by value.</returns>
        [Pure]
        public NpgsqlDateTime AddTicks(long value)
        {
            switch (_type) {
            case InternalType.Infinity:
            case InternalType.NegativeInfinity:
                return this;
            default:
                return new NpgsqlDateTime(Ticks + value, Kind);
            }
        }

        [Pure]
        public NpgsqlDateTime Subtract(NpgsqlTimeSpan interval)
        {
            return Add(-interval);
        }

        [Pure]
        public NpgsqlTimeSpan Subtract(NpgsqlDateTime timestamp)
        {
            switch (_type) {
            case InternalType.Infinity:
            case InternalType.NegativeInfinity:
                throw new ArgumentOutOfRangeException("this", "You cannot subtract infinity timestamps");
            }
            switch (timestamp._type) {
            case InternalType.Infinity:
            case InternalType.NegativeInfinity:
                throw new ArgumentOutOfRangeException("timestamp", "You cannot subtract infinity timestamps");
            }
            return new NpgsqlTimeSpan(0, _date.DaysSinceEra - timestamp._date.DaysSinceEra, _time.Ticks - timestamp._time.Ticks);
        }

        #endregion

        #region Operators

        public static NpgsqlDateTime operator +(NpgsqlDateTime timestamp, NpgsqlTimeSpan interval)
        {
            return timestamp.Add(interval);
        }

        public static NpgsqlDateTime operator +(NpgsqlTimeSpan interval, NpgsqlDateTime timestamp)
        {
            return timestamp.Add(interval);
        }

        public static NpgsqlDateTime operator -(NpgsqlDateTime timestamp, NpgsqlTimeSpan interval)
        {
            return timestamp.Subtract(interval);
        }

        public static NpgsqlTimeSpan operator -(NpgsqlDateTime x, NpgsqlDateTime y)
        {
            return x.Subtract(y);
        }

        public static bool operator ==(NpgsqlDateTime x, NpgsqlDateTime y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(NpgsqlDateTime x, NpgsqlDateTime y)
        {
            return !(x == y);
        }

        public static bool operator <(NpgsqlDateTime x, NpgsqlDateTime y)
        {
            return x.CompareTo(y) < 0;
        }

        public static bool operator >(NpgsqlDateTime x, NpgsqlDateTime y)
        {
            return x.CompareTo(y) > 0;
        }

        public static bool operator <=(NpgsqlDateTime x, NpgsqlDateTime y)
        {
            return x.CompareTo(y) <= 0;
        }

        public static bool operator >=(NpgsqlDateTime x, NpgsqlDateTime y)
        {
            return x.CompareTo(y) >= 0;
        }

        #endregion

        [Pure]
        public NpgsqlDateTime Normalize()
        {
            return Add(NpgsqlTimeSpan.Zero);
        }

        static InternalType KindToInternalType(DateTimeKind kind)
        {
            switch (kind) {
            case DateTimeKind.Unspecified:
                return InternalType.FiniteUnspecified;
            case DateTimeKind.Utc:
                return InternalType.FiniteUtc;
            case DateTimeKind.Local:
                return InternalType.FiniteLocal;
                break;
            default:
                throw PGUtil.ThrowIfReached();
            }            
        }

        enum InternalType
        {
            FiniteUnspecified,
            FiniteUtc,
            FiniteLocal,
            Infinity,
            NegativeInfinity
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql.BackendMessages;
using NpgsqlTypes;

namespace Npgsql.TypeHandlers.GeometricHandlers
{
    /// <summary>
    /// Type handler for the PostgreSQL geometric circle type.
    /// </summary>
    /// <remarks>
    /// http://www.postgresql.org/docs/current/static/datatype-geometric.html
    /// </remarks>
    [TypeMapping("circle", NpgsqlDbType.Circle, typeof(NpgsqlCircle))]
    internal class CircleHandler : TypeHandler<NpgsqlCircle>,
        ISimpleTypeReader<NpgsqlCircle>, ISimpleTypeWriter,
        ISimpleTypeReader<string>
    {
        public NpgsqlCircle Read(NpgsqlBuffer buf, int len, FieldDescription fieldDescription)
        {
            return new NpgsqlCircle(buf.ReadDouble(), buf.ReadDouble(), buf.ReadDouble());
        }

        string ISimpleTypeReader<string>.Read(NpgsqlBuffer buf, int len, FieldDescription fieldDescription)
        {
            return Read(buf, len, fieldDescription).ToString();
        }

        public int ValidateAndGetLength(object value)
        {
            return 24;
        }

        public void Write(object value, NpgsqlBuffer buf)
        {
            var s = value as string;
            var v = s != null ? NpgsqlCircle.Parse(s) : (NpgsqlCircle)value;
            buf.WriteDouble(v.X);
            buf.WriteDouble(v.Y);
            buf.WriteDouble(v.Radius);
        }
    }
}

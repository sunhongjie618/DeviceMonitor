using System;
using System.Buffers.Binary;
using System.Globalization;

namespace DeviceMonitor.PlcClient
{
    /// <summary>PLC 点位的数据类型（解码层用）。</summary>
    public enum PlcDataType
    {
        UInt16,
        Int16,
        UInt32,
        Int32,
        Float32
    }

    /// <summary>
    /// 把 Modbus 原始寄存器按点位表里的类型/字序解成双精度数值。
    /// 只依赖 System.Buffers.Binary，不依赖具体协议库，方便单元测试。
    /// </summary>
    public static class PlcValue
    {
        public static PlcDataType ParseType(string? text) => (text ?? "UInt16").Trim().ToUpperInvariant() switch
        {
            "INT16" => PlcDataType.Int16,
            "UINT32" => PlcDataType.UInt32,
            "INT32" => PlcDataType.Int32,
            "FLOAT32" or "FLOAT" or "SINGLE" => PlcDataType.Float32,
            _ => PlcDataType.UInt16
        };

        /// <summary>该类型占几个 16 位寄存器。</summary>
        public static int RegisterCount(string? dataType) => ParseType(dataType) switch
        {
            PlcDataType.UInt16 or PlcDataType.Int16 => 1,
            _ => 2
        };

        /// <summary>
        /// 从一块寄存器里按 offset 解码出一个值。offset 是块内偏移（寄存器下标）。
        /// wordSwap=true 时交换高低字（对应 Modbus 的 CDAB 字序）。
        /// </summary>
        public static double Decode(ushort[] regs, int offset, string? dataType, bool wordSwap = false, double scale = 1)
        {
            var type = ParseType(dataType);
            double raw = type switch
            {
                PlcDataType.Int16 => unchecked((short)regs[offset]),
                PlcDataType.UInt32 => ToUInt32(regs, offset, wordSwap),
                PlcDataType.Int32 => unchecked((int)ToUInt32(regs, offset, wordSwap)),
                PlcDataType.Float32 => ToFloat32(regs, offset, wordSwap),
                _ => regs[offset]
            };
            return raw * scale;
        }

        public static uint ToUInt32(ushort[] regs, int offset, bool wordSwap)
        {
            ushort hi = regs[offset], lo = regs[offset + 1];
            if (wordSwap) (hi, lo) = (lo, hi);
            return ((uint)hi << 16) | lo;
        }

        public static float ToFloat32(ushort[] regs, int offset, bool wordSwap)
        {
            Span<byte> b = stackalloc byte[4];
            ushort hi = regs[offset], lo = regs[offset + 1];
            if (wordSwap) (hi, lo) = (lo, hi);
            BinaryPrimitives.WriteUInt16BigEndian(b, hi);
            BinaryPrimitives.WriteUInt16BigEndian(b[2..], lo);
            return BinaryPrimitives.ReadSingleBigEndian(b);
        }

        /// <summary>
        /// 把「40001」这类展示地址换算成 0 基偏移（只支持保持寄存器区 4xxxx）。
        /// 手册给 1 基地址（4xxxx）就减 40001；给 0 基就原样使用。
        /// </summary>
        public static bool TryParseHoldingAddress(string? text, out ushort offset)
        {
            offset = 0;
            if (!int.TryParse((text ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var addr)
                || addr < 0)
                return false;

            offset = (ushort)(addr >= 40001 ? addr - 40001 : addr);
            return true;
        }
    }
}

﻿using System;
using System.IO.Ports;

namespace csModbusLib {
    #region CRC16 Class
    /// <summary>
    /// Static class for CRC16 compute
    /// </summary>
    public class CRC16 {
        // TODO CRC into Utils verschieben
        private const UInt16 POLY = 0xA001;
        private UInt16[] crc_tab16;

        private void InitCRC16Tab()
        {
            UInt16 crc, c;

            if (crc_tab16 == null) {
                crc_tab16 = new UInt16[256];
                for (int ii = 0; ii < 256; ii++) {
                    crc = 0;
                    c = (UInt16)ii;
                    for (int jj = 0; jj < 8; jj++) {

                        if (((crc ^ c) & 0x0001) == 0x0001)
                            crc = (UInt16)((crc >> 1) ^ POLY);
                        else
                            crc = (UInt16)(crc >> 1);

                        c = (UInt16)(c >> 1);
                    }

                    crc_tab16[ii] = crc;
                }
            }
        }

        private UInt16 UpdateCRC16(UInt16 crc, byte bt)
        {
            byte tableIndex = (byte)(crc ^ bt);
            crc >>= 8;
            crc ^= crc_tab16[tableIndex];
            return crc;
        }

        public UInt16 CalcCRC16(byte[] buffer, int offset, int length)
        {
            UInt16 crc = 0xFFFF;
            for (int ii = 0; ii < length; ii++)
                crc = UpdateCRC16(crc, buffer[offset + ii]);
            // I have to exange high low byte 
            return (UInt16)((crc >> 8) | ((crc & 0xff) << 8));
        }

        public CRC16()
        {
            InitCRC16Tab();
        }
    }

    #endregion

    public class MbRTU : MbSerial {

        private CRC16 crc16;
        public MbRTU()
        {
            Init();
        }

        public MbRTU(string port, int baudrate)
            : base(port, baudrate, 8, Parity.None, StopBits.One, Handshake.None)
        {
            Init();
        }

        public MbRTU(string port, int baudrate, int databits, Parity parity, StopBits stopbits, Handshake handshake)
            : base(port, baudrate, databits, parity, stopbits, handshake)
        {
            Init();
        }

        private void Init()
        {
            crc16 = new CRC16();
        }


        protected override bool StartOfFrameFound ()
        {
            return (sp.BytesToRead >= 2);

        }

        protected override bool Check_EndOfFrame(MbRawData RxData)
        {
            int crc_idx = RxData.EndIdx;
            ReceiveBytes(RxData, 2);

            // Check CRC
            ushort msg_crc = RxData.GetUInt16(crc_idx);
            ushort calc_crc = crc16.CalcCRC16(RxData.Data, MbRawData.ADU_OFFS, crc_idx - MbRawData.ADU_OFFS);

            return (msg_crc == calc_crc);
        }

        public override void SendFrame(MbRawData TransmitData, int Length)
        {
            ushort calc_crc = crc16.CalcCRC16(TransmitData.Data, MbRawData.ADU_OFFS, Length);
            TransmitData.PutUInt16(MbRawData.ADU_OFFS+Length, calc_crc);
            Length += 2;
            SendData(TransmitData.Data, MbRawData.ADU_OFFS, Length);
        }
    }
}

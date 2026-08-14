//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Core.USB.CDC;
using Antmicro.Renode.Extensions.Utilities.USBIP;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.UART;

namespace Antmicro.Renode.Integrations
{
    public static class RAMNCDCSerialExtensions
    {
        public static void CreateRAMNCDCSerial(this USBIPServer server, IUART uart, ushort vendorId = 0x0483,
            ushort productId = 0x5740, int? port = null, string name = null)
        {
            var cdcSerial = new RAMNCDCSerial(uart, vendorId, productId);
            server.Register(cdcSerial, port);

            var emulation = EmulationManager.Instance.CurrentEmulation;
            emulation.ExternalsManager.AddExternal(cdcSerial, name ?? "ramnCDCSerial");
        }
    }

    public class RAMNCDCSerial : IUSBDevice, IExternal
    {
        public RAMNCDCSerial(IUART uart, ushort vendorId = 0x0483, ushort productId = 0x5740)
        {
            this.uart = uart;
            rxBuffer = new Queue<byte>();
            rxBufferLock = new object();

            USBEndpoint interruptEndpoint = null;

            USBCore = new USBDeviceCore(this,
                                        classCode: USBClassCode.CommunicationsCDCControl,
                                        maximalPacketSize: PacketSize.Size16,
                                        vendorId: vendorId,
                                        productId: productId,
                                        deviceReleaseNumber: 0x0200)
                .WithConfiguration(configure: c => c
                    .WithInterface(new Antmicro.Renode.Core.USB.CDC.Interface(this,
                                                    identifier: 0,
                                                    subClassCode: 0x2,
                                                    protocol: 0x1,
                                                    descriptors: new[] {
                                                        new FunctionalDescriptor(CdcFunctionalDescriptorType.Interface, CdcFunctionalDescriptorSubtype.Header, 0x10, 0x01),
                                                        new FunctionalDescriptor(CdcFunctionalDescriptorType.Interface, CdcFunctionalDescriptorSubtype.CallManagement, 0x01, 0x01),
                                                        new FunctionalDescriptor(CdcFunctionalDescriptorType.Interface, CdcFunctionalDescriptorSubtype.AbstractControlManagement, 0x02),
                                                        new FunctionalDescriptor(CdcFunctionalDescriptorType.Interface, CdcFunctionalDescriptorSubtype.Union, 0x00, 0x01)
                                                    })
                                   .WithEndpoint(Direction.DeviceToHost,
                                                 EndpointTransferType.Interrupt,
                                                 maximumPacketSize: 0x08,
                                                 interval: 0x0a,
                                                 createdEndpoint: out interruptEndpoint))
                    .WithInterface(new USBInterface(this,
                                                    identifier: 1,
                                                    classCode: USBClassCode.CDCData,
                                                    subClassCode: 0x0,
                                                    protocol: 0x0)
                                   .WithEndpoint(id: 2,
                                                 direction: Direction.HostToDevice,
                                                 transferType: EndpointTransferType.Bulk,
                                                 maximumPacketSize: 0x40,
                                                 interval: 0x0,
                                                 createdEndpoint: out hostToDeviceEndpoint)
                                   .WithEndpoint(id: 3,
                                                 direction: Direction.DeviceToHost,
                                                 transferType: EndpointTransferType.Bulk,
                                                 maximumPacketSize: 0x40,
                                                 interval: 0x0,
                                                 createdEndpoint: out deviceToHostEndpoint)));

            interruptEndpoint.NonBlocking = true;
            deviceToHostEndpoint.NonBlocking = true;
            hostToDeviceEndpoint.DataWritten += HandleHostToDeviceData;

            uart.CharReceived += HandleUARTCharReceived;
        }

        public void Reset()
        {
            USBCore.Reset();

            lock(rxBufferLock)
            {
                rxBuffer.Clear();
            }
        }

        public USBDeviceCore USBCore { get; }

        private void HandleUARTCharReceived(byte charData)
        {
            lock(rxBufferLock)
            {
                rxBuffer.Enqueue(charData);

                if(rxBuffer.Count >= MaxPacketSize)
                {
                    FlushRxBuffer();
                }
            }
        }

        private void HandleHostToDeviceData(byte[] data)
        {
            this.Log(LogLevel.Noisy, "Received {0} bytes from host", data.Length);

            foreach(var b in data)
            {
                uart.WriteChar(b);
            }
        }

        private void FlushRxBuffer()
        {
            // Must be called with rxBufferLock held
            if(rxBuffer.Count == 0)
            {
                return;
            }

            var count = Math.Min(rxBuffer.Count, MaxPacketSize);
            var buffer = new byte[count];
            for(var i = 0; i < count; i++)
            {
                buffer[i] = rxBuffer.Dequeue();
            }

            this.Log(LogLevel.Noisy, "Sending {0} bytes to host", count);
            deviceToHostEndpoint.HandlePacket(buffer);
        }

        private USBEndpoint hostToDeviceEndpoint;
        private USBEndpoint deviceToHostEndpoint;

        private readonly IUART uart;
        private readonly Queue<byte> rxBuffer;
        private readonly object rxBufferLock;

        private const int MaxPacketSize = 64;
    }
}

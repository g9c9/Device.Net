﻿using Device.Net;
using Device.Net.Windows;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Usb.Net.Windows
{
    public class WindowsUsbDevice : WindowsDeviceBase
    {
        #region Fields
        private const int EnglishLanguageID = 1033;
        private SafeFileHandle _DeviceHandle;
        private readonly List<UsbInterface> _UsbInterfaces = new List<UsbInterface>();
        private UsbInterface _DefaultUsbInterface => _UsbInterfaces.FirstOrDefault();
        private USB_DEVICE_DESCRIPTOR _UsbDeviceDescriptor;
        #endregion

        #region Public Properties
        public string Product { get; private set; }
        #endregion

        #region Public Overrride Properties
        public override ushort WriteBufferSize => IsInitialized ? _UsbDeviceDescriptor.bMaxPacketSize0 : throw new Exception("Device has not been initialized");
        public override ushort ReadBufferSize => IsInitialized ? _UsbDeviceDescriptor.bMaxPacketSize0 : throw new Exception("Device has not been initialized");
        #endregion

        #region Constructor
        public WindowsUsbDevice(string deviceId) : base(deviceId)
        {
        }
        #endregion

        #region Private Methods
        private void Initialize()
        {
            Dispose();

            int errorCode;

            if (string.IsNullOrEmpty(DeviceId))
            {
                throw new WindowsException($"{nameof(Device.Net.DeviceDefinition)} must be specified before {nameof(InitializeAsync)} can be called.");
            }

            _DeviceHandle = APICalls.CreateFile(DeviceId, (APICalls.GenericWrite | APICalls.GenericRead), APICalls.FileShareRead | APICalls.FileShareWrite, IntPtr.Zero, APICalls.OpenExisting, APICalls.FileAttributeNormal | APICalls.FileFlagOverlapped, IntPtr.Zero);

            if (_DeviceHandle.IsInvalid)
            {
                //TODO: is error code useful here?
                errorCode = Marshal.GetLastWin32Error();
                if (errorCode > 0) throw new Exception($"Device handle no good. Error code: {errorCode}");
            }

            var isSuccess = WinUsbApiCalls.WinUsb_Initialize(_DeviceHandle, out var defaultInterfaceHandle);
            HandleError(isSuccess, "Couldn't initialize device");

            var bufferLength = (uint)Marshal.SizeOf(typeof(USB_DEVICE_DESCRIPTOR));
            isSuccess = WinUsbApiCalls.WinUsb_GetDescriptor(defaultInterfaceHandle, WinUsbApiCalls.DEFAULT_DESCRIPTOR_TYPE, 0, EnglishLanguageID, out _UsbDeviceDescriptor, bufferLength, out var lengthTransferred);
            HandleError(isSuccess, "Couldn't get device descriptor");


            byte i = 0;

            //Get the first (default) interface
            var defaultInterface = GetInterface(defaultInterfaceHandle);

            _UsbInterfaces.Add(defaultInterface);

            while (true)
            {
                isSuccess = WinUsbApiCalls.WinUsb_GetAssociatedInterface(defaultInterfaceHandle, i, out var interfacePointer);
                if (!isSuccess)
                {
                    errorCode = Marshal.GetLastWin32Error();
                    if (errorCode == APICalls.ERROR_NO_MORE_ITEMS) break;

                    throw new Exception($"Could not enumerate interfaces for device {DeviceId}. Error code: { errorCode}");
                }

                var associatedInterface = GetInterface(interfacePointer);

                _UsbInterfaces.Add(associatedInterface);

                i++;
            }

            IsInitialized = true;
        }
        #endregion

        #region Public Methods
        public override async Task InitializeAsync()
        {
            await Task.Run(Initialize);
            RaiseConnected();
        }

        public override async Task<byte[]> ReadAsync()
        {
            return await Task.Run(() =>
            {
                var bytes = new byte[ReadBufferSize];
                //TODO: Allow for different interfaces and pipes...
                var isSuccess = WinUsbApiCalls.WinUsb_ReadPipe(_DefaultUsbInterface.Handle, _DefaultUsbInterface.ReadPipe.WINUSB_PIPE_INFORMATION.PipeId, bytes, ReadBufferSize, out var bytesRead, IntPtr.Zero);
                HandleError(isSuccess, "Couldn't read data");
                Tracer?.Trace(false, bytes);
                return bytes;
            });
        }

        public override async Task WriteAsync(byte[] data)
        {
            await Task.Run(() =>
            {
                if (data.Length > WriteBufferSize)
                {
                    throw new Exception($"Data is longer than {WriteBufferSize} bytes which is the device's max buffer size.");
                }

                //TODO: Allow for different interfaces and pipes...
                var isSuccess = WinUsbApiCalls.WinUsb_WritePipe(_DefaultUsbInterface.Handle, _DefaultUsbInterface.WritePipe.WINUSB_PIPE_INFORMATION.PipeId, data, (uint)data.Length, out var bytesWritten, IntPtr.Zero);
                HandleError(isSuccess, "Couldn't write data");
                Tracer?.Trace(true, data);
            });
        }

        public override void Dispose()
        {
            IsInitialized = false;

            foreach (var usbInterface in _UsbInterfaces)
            {
                usbInterface.Dispose();
            }
            _UsbInterfaces.Clear();

            _DeviceHandle?.Dispose();

            base.Dispose();
        }
        #endregion

        #region Private Static Methods
        private static UsbInterface GetInterface(SafeFileHandle interfaceHandle)
        {
            var retVal = new UsbInterface { Handle = interfaceHandle };
            var isSuccess = WinUsbApiCalls.WinUsb_QueryInterfaceSettings(interfaceHandle, 0, out var interfaceDescriptor);
            HandleError(isSuccess, "Couldn't query interface");

            retVal.USB_INTERFACE_DESCRIPTOR = interfaceDescriptor;

            for (byte i = 0; i < interfaceDescriptor.bNumEndpoints; i++)
            {
                isSuccess = WinUsbApiCalls.WinUsb_QueryPipe(interfaceHandle, 0, i, out var pipeInfo);
                HandleError(isSuccess, "Couldn't query pipe");
                retVal.UsbInterfacePipes.Add(new UsbInterfacePipe { WINUSB_PIPE_INFORMATION = pipeInfo });
            }

            return retVal;
        }
        #endregion
    }
}

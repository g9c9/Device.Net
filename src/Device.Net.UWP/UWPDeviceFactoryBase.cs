﻿using Device.Net.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using wde = Windows.Devices.Enumeration;

namespace Device.Net.UWP
{
    public abstract class UWPDeviceFactoryBase
    {
        #region Fields
        //TODO: Should we allow enumerating devices that are defined but not connected? This is very good for situations where we need the Id of the device before it is physically connected.
        protected const string InterfaceEnabledPart = "AND System.Devices.InterfaceEnabled:=System.StructuredQueryType.Boolean#True";
        #endregion

        #region Protected Abstraction Properties
        protected abstract string VendorFilterName { get; }
        protected abstract string ProductFilterName { get; }
        #endregion

        #region Public Abstract Properties
        public abstract DeviceType DeviceType { get; }
        #endregion

        #region Protected Abstract Methods
        protected abstract string GetAqsFilter(uint? vendorId, uint? productId);
        #endregion

        #region Abstraction Methods
        protected string GetVendorPart(uint? vendorId)
        {
            string vendorPart = null;
            if (vendorId.HasValue) vendorPart = $"AND {VendorFilterName}:={vendorId.Value}";
            return vendorPart;
        }

        protected string GetProductPart(uint? productId)
        {
            string productPart = null;
            if (productId.HasValue) productPart = $"AND {ProductFilterName}:={productId.Value}";
            return productPart;
        }
        #endregion

        #region Public Methods
        public async Task<IEnumerable<DeviceDefinition>> GetConnectedDeviceDefinitions(uint? vendorId, uint? productId)
        {
            var aqsFilter = GetAqsFilter(vendorId, productId);

            var deviceInformationCollection = await wde.DeviceInformation.FindAllAsync(aqsFilter).AsTask();

            //TODO: Use the properties to fill in the device definition stuff
            //foreach (var deviceInformation in deviceInformationCollection)
            //{
            //    System.Diagnostics.Debug.WriteLine($"{deviceInformation.Id} {string.Join(", ", deviceInformation.Properties.Select(p => p.ToString()))}");
            //}

            return deviceInformationCollection.Select(d => WindowsDeviceFactoryBase.GetDeviceDefinitionFromWindowsDeviceId(d.Id, DeviceType)).ToList();
        }
        #endregion
    }
}
﻿using System;
using HA4IoT.Actuators;
using HA4IoT.Actuators.Fans;
using HA4IoT.Actuators.Lamps;
using HA4IoT.Areas;
using HA4IoT.Automations;
using HA4IoT.Components.Adapters.MqttBased;
using HA4IoT.Contracts.Areas;
using HA4IoT.Contracts.Components.Adapters;
using HA4IoT.Contracts.Core;
using HA4IoT.Contracts.Hardware;
using HA4IoT.Contracts.Hardware.DeviceMessaging;
using HA4IoT.Contracts.Logging;
using HA4IoT.Contracts.Messaging;
using HA4IoT.Contracts.Scheduling;
using HA4IoT.Contracts.Settings;
using HA4IoT.Hardware.Drivers.CCTools;
using HA4IoT.Hardware.Drivers.CCTools.Devices;
using HA4IoT.Sensors;
using HA4IoT.Sensors.MotionDetectors;

namespace HA4IoT.Controller.Main.Main.Rooms
{
    internal class UpperBathroomConfiguration
    {
        private readonly IMessageBrokerService _messageBroker;
        private readonly IDeviceMessageBrokerService _deviceMessageBrokerService;
        private readonly ILogService _logService;
        private readonly CCToolsDeviceService _ccToolsBoardService;
        private readonly IDeviceRegistryService _deviceService;
        private readonly ISchedulerService _schedulerService;
        private readonly IAreaRegistryService _areaService;
        private readonly ISettingsService _settingsService;
        private readonly AutomationFactory _automationFactory;
        private readonly ActuatorFactory _actuatorFactory;
        private readonly SensorFactory _sensorFactory;

        private enum UpperBathroom
        {
            TemperatureSensor,
            HumiditySensor,
            MotionDetector,

            LightCeilingDoor,
            LightCeilingEdge,
            LightCeilingMirrorCabinet,
            LampMirrorCabinet,

            Fan,
            FanAutomation,

            CombinedCeilingLights,
            CombinedCeilingLightsAutomation
        }

        public UpperBathroomConfiguration(
            CCToolsDeviceService ccToolsBoardService,
            IDeviceRegistryService deviceService,
            ISchedulerService schedulerService,
            IAreaRegistryService areaService,
            ISettingsService settingsService,
            AutomationFactory automationFactory,
            ActuatorFactory actuatorFactory,
            SensorFactory sensorFactory,
            IMessageBrokerService messageBroker,
            IDeviceMessageBrokerService deviceMessageBrokerService,
            ILogService logService)
        {
            _messageBroker = messageBroker;
            _deviceMessageBrokerService = deviceMessageBrokerService;
            _logService = logService;
            _ccToolsBoardService = ccToolsBoardService ?? throw new ArgumentNullException(nameof(ccToolsBoardService));
            _deviceService = deviceService ?? throw new ArgumentNullException(nameof(deviceService));
            _schedulerService = schedulerService ?? throw new ArgumentNullException(nameof(schedulerService));
            _areaService = areaService ?? throw new ArgumentNullException(nameof(areaService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _automationFactory = automationFactory ?? throw new ArgumentNullException(nameof(automationFactory));
            _actuatorFactory = actuatorFactory ?? throw new ArgumentNullException(nameof(actuatorFactory));
            _sensorFactory = sensorFactory ?? throw new ArgumentNullException(nameof(sensorFactory));
            _messageBroker = messageBroker ?? throw new ArgumentNullException(nameof(sensorFactory));
        }

        public void Apply()
        {
            var hsrel5 = (HSREL5)_ccToolsBoardService.RegisterDevice(CCToolsDeviceType.HSRel5, InstalledDevice.UpperBathroomHSREL5.ToString(), 61);
            var input5 = _deviceService.GetDevice<HSPE16InputOnly>(InstalledDevice.Input5.ToString());

            var area = _areaService.RegisterArea(Room.UpperBathroom);

            _sensorFactory.RegisterTemperatureSensor(area, UpperBathroom.TemperatureSensor,
                new MqttBasedNumericSensorAdapter("sensors-bridge/temperature/7", _deviceMessageBrokerService, _logService));

            _sensorFactory.RegisterHumiditySensor(area, UpperBathroom.HumiditySensor,
                new MqttBasedNumericSensorAdapter("sensors-bridge/humidity/7", _deviceMessageBrokerService, _logService));

            _sensorFactory.RegisterMotionDetector(area, UpperBathroom.MotionDetector, input5.GetInput(15));

            _actuatorFactory.RegisterFan(area, UpperBathroom.Fan, new UpperBathroomFanAdapter(hsrel5));

            _actuatorFactory.RegisterLamp(area, UpperBathroom.LightCeilingDoor, hsrel5.GetOutput(0));
            _actuatorFactory.RegisterLamp(area, UpperBathroom.LightCeilingEdge, hsrel5.GetOutput(1));
            _actuatorFactory.RegisterLamp(area, UpperBathroom.LightCeilingMirrorCabinet, hsrel5.GetOutput(2));
            _actuatorFactory.RegisterLamp(area, UpperBathroom.LampMirrorCabinet, hsrel5.GetOutput(3));

            var combinedLights = _actuatorFactory.RegisterLogicalComponent(area, UpperBathroom.CombinedCeilingLights)
                    .WithComponent(area.GetLamp(UpperBathroom.LightCeilingDoor))
                    .WithComponent(area.GetLamp(UpperBathroom.LightCeilingEdge))
                    .WithComponent(area.GetLamp(UpperBathroom.LightCeilingMirrorCabinet))
                    .WithComponent(area.GetLamp(UpperBathroom.LampMirrorCabinet));

            _automationFactory.RegisterTurnOnAndOffAutomation(area, UpperBathroom.CombinedCeilingLightsAutomation)
                .WithTrigger(area.GetMotionDetector(UpperBathroom.MotionDetector))
                .WithTarget(combinedLights);

            new BathroomFanAutomation(
                $"{area.Id}.{UpperBathroom.FanAutomation}",
                area.GetFan(UpperBathroom.Fan),
                _schedulerService,
                _settingsService,
                _messageBroker)
                .WithTrigger(area.GetMotionDetector(UpperBathroom.MotionDetector));
        }

        private class UpperBathroomFanAdapter : IFanAdapter
        {
            private readonly IBinaryOutput _relay1;
            private readonly IBinaryOutput _relay2;

            public int MaxLevel { get; } = 2;

            public UpperBathroomFanAdapter(HSREL5 hsrel5)
            {
                _relay1 = hsrel5[HSREL5Pin.Relay4];
                _relay2 = hsrel5[HSREL5Pin.GPIO0];
            }

            public void SetState(int level, params IHardwareParameter[] parameters)
            {
                switch (level)
                {
                    case 0:
                        {
                            _relay1.Write(BinaryState.Low);
                            _relay2.Write(BinaryState.Low);
                            break;
                        }

                    case 1:
                        {
                            _relay1.Write(BinaryState.High);
                            _relay2.Write(BinaryState.Low);
                            break;
                        }

                    case 2:
                        {
                            _relay1.Write(BinaryState.High);
                            _relay2.Write(BinaryState.High);
                            break;
                        }

                    default:
                        {
                            throw new NotSupportedException();
                        }
                }
            }
        }
    }
}

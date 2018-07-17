﻿using System;
using HA4IoT.Actuators;
using HA4IoT.Actuators.Lamps;
using HA4IoT.Areas;
using HA4IoT.Automations;
using HA4IoT.Components;
using HA4IoT.Components.Adapters.MqttBased;
using HA4IoT.Components.Adapters.PortBased;
using HA4IoT.Contracts.Areas;
using HA4IoT.Contracts.Core;
using HA4IoT.Contracts.Hardware;
using HA4IoT.Contracts.Hardware.DeviceMessaging;
using HA4IoT.Contracts.Logging;
using HA4IoT.Contracts.Messaging;
using HA4IoT.Contracts.Scheduling;
using HA4IoT.Contracts.Settings;
using HA4IoT.Hardware.Drivers.CCTools.Devices;
using HA4IoT.Scheduling;
using HA4IoT.Sensors;
using HA4IoT.Sensors.Buttons;
using HA4IoT.Sensors.MotionDetectors;

namespace HA4IoT.Controller.Main.Main.Rooms
{
    internal class LowerBathroomConfiguration
    {
        private readonly ISettingsService _settingsService;
        private readonly IDeviceRegistryService _deviceService;
        private readonly ISchedulerService _schedulerService;
        private readonly IAreaRegistryService _areaService;
        private readonly AutomationFactory _automationFactory;
        private readonly ActuatorFactory _actuatorFactory;
        private readonly SensorFactory _sensorFactory;
        private readonly IMessageBrokerService _messageBroker;
        private readonly IDeviceMessageBrokerService _deviceMessageBrokerService;
        private readonly ILogService _logService;

        private IScheduledAction _bathmodeResetDelayedAction;

        public enum LowerBathroom
        {
            TemperatureSensor,
            HumiditySensor,
            MotionDetector,

            LightCeilingDoor,
            LightCeilingMiddle,
            LightCeilingWindow,
            // Another light is available!
            CombinedLights,
            CombinedLightsAutomation,

            StartBathmodeButton,
            LampMirror,

            Window
        }

        public LowerBathroomConfiguration(
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
            _messageBroker = messageBroker ?? throw new ArgumentNullException(nameof(messageBroker));
            _deviceMessageBrokerService = deviceMessageBrokerService;
            _logService = logService;
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _deviceService = deviceService ?? throw new ArgumentNullException(nameof(deviceService));
            _schedulerService = schedulerService ?? throw new ArgumentNullException(nameof(schedulerService));
            _areaService = areaService ?? throw new ArgumentNullException(nameof(areaService));
            _automationFactory = automationFactory ?? throw new ArgumentNullException(nameof(automationFactory));
            _actuatorFactory = actuatorFactory ?? throw new ArgumentNullException(nameof(actuatorFactory));
            _sensorFactory = sensorFactory ?? throw new ArgumentNullException(nameof(sensorFactory));
        }

        public void Apply()
        {
            var hspe16_FloorAndLowerBathroom = _deviceService.GetDevice<HSPE16OutputOnly>(InstalledDevice.LowerFloorAndLowerBathroomHSPE16.ToString());
            var input3 = _deviceService.GetDevice<HSPE16InputOnly>(InstalledDevice.Input3.ToString());

            var area = _areaService.RegisterArea(Room.LowerBathroom);

            _sensorFactory.RegisterWindow(area, LowerBathroom.Window, new PortBasedWindowAdapter(input3.GetInput(13), input3.GetInput(14)));

            _sensorFactory.RegisterTemperatureSensor(area, LowerBathroom.TemperatureSensor,
                new MqttBasedNumericSensorAdapter("sensors-bridge/temperature/4", _deviceMessageBrokerService, _logService));

            _sensorFactory.RegisterHumiditySensor(area, LowerBathroom.HumiditySensor,
                new MqttBasedNumericSensorAdapter("sensors-bridge/humidity/4", _deviceMessageBrokerService, _logService));

            _sensorFactory.RegisterMotionDetector(area, LowerBathroom.MotionDetector, input3.GetInput(15));

            var bathModeButton = _sensorFactory.RegisterVirtualButton(area, LowerBathroom.StartBathmodeButton);
            bathModeButton.CreatePressedShortTrigger(_messageBroker).Attach(() => StartBathode(area));

            _actuatorFactory.RegisterLamp(area, LowerBathroom.LightCeilingDoor,
                hspe16_FloorAndLowerBathroom.GetOutput(0).WithInvertedState());

            _actuatorFactory.RegisterLamp(area, LowerBathroom.LightCeilingMiddle,
                hspe16_FloorAndLowerBathroom.GetOutput(1).WithInvertedState());

            _actuatorFactory.RegisterLamp(area, LowerBathroom.LightCeilingWindow,
                hspe16_FloorAndLowerBathroom.GetOutput(2).WithInvertedState());

            _actuatorFactory.RegisterLamp(area, LowerBathroom.LampMirror,
                hspe16_FloorAndLowerBathroom.GetOutput(4).WithInvertedState());

            _actuatorFactory.RegisterLogicalComponent(area, LowerBathroom.CombinedLights)
                .WithComponent(area.GetLamp(LowerBathroom.LightCeilingDoor))
                .WithComponent(area.GetLamp(LowerBathroom.LightCeilingMiddle))
                .WithComponent(area.GetLamp(LowerBathroom.LightCeilingWindow))
                .WithComponent(area.GetLamp(LowerBathroom.LampMirror));

            _automationFactory.RegisterTurnOnAndOffAutomation(area, LowerBathroom.CombinedLightsAutomation)
                .WithTrigger(area.GetMotionDetector(LowerBathroom.MotionDetector))
                .WithTarget(area.GetComponent(LowerBathroom.CombinedLights));
        }

        private void StartBathode(IArea bathroom)
        {
            var motionDetector = bathroom.GetMotionDetector(LowerBathroom.MotionDetector);
            _settingsService.SetComponentEnabledState(motionDetector, false);

            bathroom.GetLamp(LowerBathroom.LightCeilingDoor).TryTurnOn();
            bathroom.GetLamp(LowerBathroom.LightCeilingMiddle).TryTurnOff();
            bathroom.GetLamp(LowerBathroom.LightCeilingWindow).TryTurnOff();
            bathroom.GetLamp(LowerBathroom.LampMirror).TryTurnOff();

            _bathmodeResetDelayedAction?.Cancel();
            _bathmodeResetDelayedAction = ScheduledAction.Schedule(TimeSpan.FromHours(1), () =>
            {
                bathroom.GetLamp(LowerBathroom.LightCeilingDoor).TryTurnOff();
                _settingsService.SetComponentEnabledState(motionDetector, true);
            });
        }
    }
}

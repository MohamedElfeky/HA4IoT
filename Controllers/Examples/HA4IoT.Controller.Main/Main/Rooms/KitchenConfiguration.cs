﻿using System;
using HA4IoT.Actuators;
using HA4IoT.Actuators.Connectors;
using HA4IoT.Actuators.Lamps;
using HA4IoT.Actuators.RollerShutters;
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
using HA4IoT.Hardware.Drivers.CCTools;
using HA4IoT.Hardware.Drivers.CCTools.Devices;
using HA4IoT.Sensors;
using HA4IoT.Sensors.Buttons;
using HA4IoT.Sensors.MotionDetectors;

namespace HA4IoT.Controller.Main.Main.Rooms
{
    internal class KitchenConfiguration
    {
        private readonly ISystemEventsService _systemEventsService;
        private readonly IAreaRegistryService _areaService;
        private readonly IDeviceRegistryService _deviceService;
        private readonly CCToolsDeviceService _ccToolsBoardService;
        private readonly AutomationFactory _automationFactory;
        private readonly ActuatorFactory _actuatorFactory;
        private readonly SensorFactory _sensorFactory;
        private readonly IMessageBrokerService _messageBroker;
        private readonly IDeviceMessageBrokerService _deviceMessageBrokerService;
        private readonly ILogService _logService;

        public enum Kitchen
        {
            TemperatureSensor,
            HumiditySensor,
            MotionDetector,

            LightCeilingMiddle,
            LightCeilingWall,
            LightCeilingWindow,
            LightCeilingDoor,
            LightCeilingPassageOuter,
            LightCeilingPassageInner,
            LightKitchenette,
            CombinedAutomaticLights,
            CombinedAutomaticLightsAutomation,

            RollerShutter,
            RollerShutterButtonUp,
            RollerShutterButtonDown,
            RollerShutterAutomation,

            ButtonPassage,
            ButtonKitchenette,

            SocketWall,
            SocketKitchenette,

            SocketCeiling1, // Über Hängeschrank
            SocketCeiling2, // Bei Dunstabzug

            Window
        }

        public KitchenConfiguration(
            ISystemEventsService systemEventsService,
            IAreaRegistryService areaService,
            IDeviceRegistryService deviceService,
            CCToolsDeviceService ccToolsDeviceService,
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
            _systemEventsService = systemEventsService ?? throw new ArgumentNullException(nameof(systemEventsService));
            _areaService = areaService ?? throw new ArgumentNullException(nameof(areaService));
            _deviceService = deviceService ?? throw new ArgumentNullException(nameof(deviceService));
            _ccToolsBoardService = ccToolsDeviceService ?? throw new ArgumentNullException(nameof(ccToolsDeviceService));
            _automationFactory = automationFactory ?? throw new ArgumentNullException(nameof(automationFactory));
            _actuatorFactory = actuatorFactory ?? throw new ArgumentNullException(nameof(actuatorFactory));
            _sensorFactory = sensorFactory ?? throw new ArgumentNullException(nameof(sensorFactory));
        }

        public void Apply()
        {
            var hsrel5 = (HSREL5)_ccToolsBoardService.RegisterDevice(CCToolsDeviceType.HSRel5, InstalledDevice.KitchenHSREL5.ToString(), 58);
            var hspe8 = (HSPE8OutputOnly)_ccToolsBoardService.RegisterDevice(CCToolsDeviceType.HSPE8_OutputOnly, InstalledDevice.KitchenHSPE8.ToString(), 39);

            var input0 = _deviceService.GetDevice<HSPE16InputOnly>(InstalledDevice.Input0.ToString());
            var input1 = _deviceService.GetDevice<HSPE16InputOnly>(InstalledDevice.Input1.ToString());
            var input2 = _deviceService.GetDevice<HSPE16InputOnly>(InstalledDevice.Input2.ToString());

            var area = _areaService.RegisterArea(Room.Kitchen);

            _sensorFactory.RegisterWindow(area, Kitchen.Window, new PortBasedWindowAdapter(input0.GetInput(6), input0.GetInput(7)));

            _sensorFactory.RegisterTemperatureSensor(area, Kitchen.TemperatureSensor,
                new MqttBasedNumericSensorAdapter("sensors-bridge/temperature/1", _deviceMessageBrokerService, _logService));

            _sensorFactory.RegisterHumiditySensor(area, Kitchen.HumiditySensor,
                new MqttBasedNumericSensorAdapter("sensors-bridge/humidity/1", _deviceMessageBrokerService, _logService));

            _sensorFactory.RegisterMotionDetector(area, Kitchen.MotionDetector, input1.GetInput(8));

            _actuatorFactory.RegisterLamp(area, Kitchen.LightCeilingMiddle, hsrel5[HSREL5Pin.GPIO0].WithInvertedState());
            _actuatorFactory.RegisterLamp(area, Kitchen.LightCeilingWindow, hsrel5[HSREL5Pin.GPIO1].WithInvertedState());
            _actuatorFactory.RegisterLamp(area, Kitchen.LightCeilingWall, hsrel5[HSREL5Pin.GPIO2].WithInvertedState());
            _actuatorFactory.RegisterLamp(area, Kitchen.LightCeilingDoor, hspe8[HSPE8Pin.GPIO0].WithInvertedState());
            _actuatorFactory.RegisterLamp(area, Kitchen.LightCeilingPassageInner, hspe8[HSPE8Pin.GPIO1].WithInvertedState());
            _actuatorFactory.RegisterLamp(area, Kitchen.LightCeilingPassageOuter, hspe8[HSPE8Pin.GPIO2].WithInvertedState());
            _actuatorFactory.RegisterLamp(area, Kitchen.LightKitchenette, new RgbDeviceAdapter("kitchen-rgb/$patch/rgb", _deviceMessageBrokerService));

            _actuatorFactory.RegisterSocket(area, Kitchen.SocketKitchenette, hsrel5[HSREL5Pin.Relay1]); // 0?
            _actuatorFactory.RegisterSocket(area, Kitchen.SocketWall, hsrel5[HSREL5Pin.Relay2]);
            _actuatorFactory.RegisterSocket(area, Kitchen.SocketCeiling1, hspe8[HSPE8Pin.GPIO3].WithInvertedState());
            _actuatorFactory.RegisterSocket(area, Kitchen.SocketCeiling2, hspe8[HSPE8Pin.GPIO4].WithInvertedState());

            _systemEventsService.StartupCompleted += (s, e) =>
            {
                area.GetComponent(Kitchen.SocketCeiling1).TryTurnOn();
            };

            _actuatorFactory.RegisterRollerShutter(area, Kitchen.RollerShutter, hsrel5[HSREL5Pin.Relay4], hsrel5[HSREL5Pin.Relay3]);

            _sensorFactory.RegisterButton(area, Kitchen.ButtonKitchenette, input1.GetInput(11));
            _sensorFactory.RegisterButton(area, Kitchen.ButtonPassage, input1.GetInput(9));
            _sensorFactory.RegisterButton(area, Kitchen.RollerShutterButtonUp, input2.GetInput(15));
            _sensorFactory.RegisterButton(area, Kitchen.RollerShutterButtonDown, input2.GetInput(14));

            area.GetButton(Kitchen.ButtonKitchenette).CreatePressedShortTrigger(_messageBroker).Attach(() => area.GetLamp(Kitchen.LightCeilingMiddle).TryTogglePowerState());
            area.GetButton(Kitchen.ButtonPassage).CreatePressedShortTrigger(_messageBroker).Attach(() => area.GetLamp(Kitchen.LightCeilingMiddle).TryTogglePowerState());

            _automationFactory.RegisterRollerShutterAutomation(area, Kitchen.RollerShutterAutomation)
                .WithRollerShutters(area.GetRollerShutter(Kitchen.RollerShutter));

            area.GetRollerShutter(Kitchen.RollerShutter).ConnectWith(
                area.GetButton(Kitchen.RollerShutterButtonUp), area.GetButton(Kitchen.RollerShutterButtonDown), _messageBroker);

            area.GetButton(Kitchen.RollerShutterButtonUp).CreatePressedLongTrigger(_messageBroker).Attach(() =>
            {
                var light = area.GetComponent(Kitchen.LightKitchenette);
                light.TryTogglePowerState();
                light.TrySetColor(0D, 0D, 1D);
            });

            _actuatorFactory.RegisterLogicalComponent(area, Kitchen.CombinedAutomaticLights)
                .WithComponent(area.GetLamp(Kitchen.LightCeilingWall))
                .WithComponent(area.GetLamp(Kitchen.LightCeilingDoor))
                .WithComponent(area.GetLamp(Kitchen.LightCeilingWindow));

            _automationFactory.RegisterTurnOnAndOffAutomation(area, Kitchen.CombinedAutomaticLightsAutomation)
                .WithTrigger(area.GetMotionDetector(Kitchen.MotionDetector))
                .WithTarget(area.GetComponent(Kitchen.CombinedAutomaticLights))
                .WithEnabledAtNight();
        }
    }
}

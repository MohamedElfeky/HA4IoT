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
using HA4IoT.Contracts.Components.Adapters;
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
    internal class BedroomConfiguration
    {
        private readonly IDeviceRegistryService _deviceService;
        private readonly IAreaRegistryService _areaService;
        private readonly CCToolsDeviceService _ccToolsBoardService;
        private readonly ActuatorFactory _actuatorFactory;
        private readonly SensorFactory _sensorFactory;
        private readonly AutomationFactory _automationFactory;
        private readonly IMessageBrokerService _messageBroker;
        private readonly IDeviceMessageBrokerService _deviceMessageBrokerService;
        private readonly ILogService _logService;

        private enum Bedroom
        {
            TemperatureSensor,
            HumiditySensor,
            MotionDetector,

            LightCeiling,
            LightCeilingAutomation,
            LightCeilingWindow,
            LightCeilingWall,

            LampBedLeft,
            LampBedRight,
            RgbLight,

            SocketWindowLeft,
            SocketWindowRight,
            SocketWall,
            SocketWallEdge,
            SocketBedLeft,
            SocketBedRight,

            ButtonDoor,
            ButtonWindowUpper,
            ButtonWindowLower,

            ButtonBedLeftInner,
            ButtonBedLeftOuter,
            ButtonBedRightInner,
            ButtonBedRightOuter,

            RollerShutterButtonsUpperUp,
            RollerShutterButtonsUpperDown,
            RollerShutterButtonsLowerUp,
            RollerShutterButtonsLowerDown,

            RollerShutterLeft,
            RollerShutterLeftAutomation,
            RollerShutterRight,
            RollerShutterRightAutomation,

            Fan,

            CombinedCeilingLights,

            WindowLeft,
            WindowRight
        }

        public BedroomConfiguration(
            IDeviceRegistryService deviceService,
            IAreaRegistryService areaService,
            CCToolsDeviceService ccToolsBoardService,
            ActuatorFactory actuatorFactory,
            SensorFactory sensorFactory,
            AutomationFactory automationFactory,
            IMessageBrokerService messageBroker,
            IDeviceMessageBrokerService deviceMessageBroker,
            ILogService logService)
        {
            _deviceService = deviceService ?? throw new ArgumentNullException(nameof(deviceService));
            _areaService = areaService ?? throw new ArgumentNullException(nameof(areaService));
            _ccToolsBoardService = ccToolsBoardService ?? throw new ArgumentNullException(nameof(ccToolsBoardService));
            _actuatorFactory = actuatorFactory ?? throw new ArgumentNullException(nameof(actuatorFactory));
            _sensorFactory = sensorFactory ?? throw new ArgumentNullException(nameof(sensorFactory));
            _automationFactory = automationFactory ?? throw new ArgumentNullException(nameof(automationFactory));
            _messageBroker = messageBroker ?? throw new ArgumentNullException(nameof(messageBroker));
            _deviceMessageBrokerService = deviceMessageBroker ?? throw new ArgumentNullException(nameof(deviceMessageBroker));
            _logService = logService;
        }

        public void Apply()
        {
            var hsrel5 = (HSREL5)_ccToolsBoardService.RegisterDevice(CCToolsDeviceType.HSRel5, InstalledDevice.BedroomHSREL5.ToString(), 38);
            var hsrel8 = (HSREL8)_ccToolsBoardService.RegisterDevice(CCToolsDeviceType.HSRel8, InstalledDevice.BedroomHSREL8.ToString(), 21);
            var input5 = _deviceService.GetDevice<HSPE16InputOnly>(InstalledDevice.Input5.ToString());

            var area = _areaService.RegisterArea(Room.Bedroom);

            _sensorFactory.RegisterWindow(area, Bedroom.WindowLeft, new PortBasedWindowAdapter(input5.GetInput(2)));
            _sensorFactory.RegisterWindow(area, Bedroom.WindowRight, new PortBasedWindowAdapter(input5.GetInput(3)));

            _sensorFactory.RegisterTemperatureSensor(area, Bedroom.TemperatureSensor,
                new MqttBasedNumericSensorAdapter("sensors-bridge/temperature/8", _deviceMessageBrokerService, _logService));

            _sensorFactory.RegisterHumiditySensor(area, Bedroom.HumiditySensor,
                new MqttBasedNumericSensorAdapter("sensors-bridge/humidity/8", _deviceMessageBrokerService, _logService));

            _sensorFactory.RegisterMotionDetector(area, Bedroom.MotionDetector, input5.GetInput(12));
           
            _sensorFactory.RegisterButton(area, Bedroom.RollerShutterButtonsLowerUp, input5.GetInput(4));
            _sensorFactory.RegisterButton(area, Bedroom.RollerShutterButtonsLowerDown, input5.GetInput(5));
            _sensorFactory.RegisterButton(area, Bedroom.RollerShutterButtonsUpperUp, input5.GetInput(6));
            _sensorFactory.RegisterButton(area, Bedroom.RollerShutterButtonsUpperDown, input5.GetInput(7));

            _sensorFactory.RegisterButton(area, Bedroom.ButtonWindowUpper, input5.GetInput(10));
            _sensorFactory.RegisterButton(area, Bedroom.ButtonDoor, input5.GetInput(11));
            _sensorFactory.RegisterButton(area, Bedroom.ButtonWindowLower, input5.GetInput(13));

            _actuatorFactory.RegisterLamp(area, Bedroom.LightCeiling, hsrel5.GetOutput(5).WithInvertedState());
            _actuatorFactory.RegisterLamp(area, Bedroom.LightCeilingWindow, hsrel5.GetOutput(6).WithInvertedState());
            _actuatorFactory.RegisterLamp(area, Bedroom.LightCeilingWall, hsrel5.GetOutput(7).WithInvertedState());

            _actuatorFactory.RegisterSocket(area, Bedroom.SocketWindowLeft, hsrel5[HSREL5Pin.Relay0]);
            _actuatorFactory.RegisterSocket(area, Bedroom.SocketWindowRight, hsrel5[HSREL5Pin.Relay1]);
            _actuatorFactory.RegisterSocket(area, Bedroom.SocketWall, hsrel5[HSREL5Pin.Relay2]);
            _actuatorFactory.RegisterSocket(area, Bedroom.SocketWallEdge, hsrel5[HSREL5Pin.Relay3]);

            // Bed components
            //_sensorFactory.RegisterButton(area, Bedroom.ButtonBedLeftOuter, input4.GetInput(0));
            //_sensorFactory.RegisterButton(area, Bedroom.ButtonBedRightInner, input4.GetInput(1));
            //_sensorFactory.RegisterButton(area, Bedroom.ButtonBedLeftInner, input4.GetInput(2));
            //_sensorFactory.RegisterButton(area, Bedroom.ButtonBedRightOuter, input4.GetInput(3));
            //_actuatorFactory.RegisterLamp(area, Bedroom.LampBedLeft, hsrel5.GetOutput(4));
            //_actuatorFactory.RegisterLamp(area, Bedroom.LampBedRight, hsrel8.GetOutput(8).WithInvertedState());
            //_actuatorFactory.RegisterLamp(area, Bedroom.RgbLight, _outpostDeviceService.CreateRgbStripAdapter("RGBSBR1"));
            //_actuatorFactory.RegisterSocket(area, Bedroom.SocketBedLeft, hsrel8.GetOutput(7));
            //_actuatorFactory.RegisterSocket(area, Bedroom.SocketBedRight, hsrel8.GetOutput(9));
            _actuatorFactory.RegisterLamp(area, Bedroom.LampBedLeft, new DeviceBinaryOutput("bed/$patch/lamp-left", _deviceMessageBrokerService));
            _actuatorFactory.RegisterLamp(area, Bedroom.LampBedRight, new DeviceBinaryOutput("bed/$patch/lamp-right", _deviceMessageBrokerService));
            _actuatorFactory.RegisterSocket(area, Bedroom.SocketBedLeft, new DeviceBinaryOutput("bed/$patch/socket-left", _deviceMessageBrokerService));
            _actuatorFactory.RegisterSocket(area, Bedroom.SocketBedRight, new DeviceBinaryOutput("bed/$patch/socket-right", _deviceMessageBrokerService));
            _actuatorFactory.RegisterLamp(area, Bedroom.RgbLight, new RgbDeviceAdapter("bed/$patch/rgb", _deviceMessageBrokerService));
            _sensorFactory.RegisterButton(area, Bedroom.ButtonBedLeftInner, new DeviceBinaryInput("bed/button-left-right", _deviceMessageBrokerService));
            _sensorFactory.RegisterButton(area, Bedroom.ButtonBedLeftOuter, new DeviceBinaryInput("bed/button-left-left", _deviceMessageBrokerService));
            _sensorFactory.RegisterButton(area, Bedroom.ButtonBedRightInner, new DeviceBinaryInput("bed/button-right-left", _deviceMessageBrokerService));
            _sensorFactory.RegisterButton(area, Bedroom.ButtonBedRightOuter, new DeviceBinaryInput("bed/button-right-right", _deviceMessageBrokerService));

            _actuatorFactory.RegisterRollerShutter(area, Bedroom.RollerShutterLeft, hsrel8[HSREL8Pin.Relay6], hsrel8[HSREL8Pin.Relay5]);
            _actuatorFactory.RegisterRollerShutter(area, Bedroom.RollerShutterRight, hsrel8[HSREL8Pin.Relay3], hsrel8[HSREL8Pin.Relay4]);

            area.GetRollerShutter(Bedroom.RollerShutterLeft)
                .ConnectWith(area.GetButton(Bedroom.RollerShutterButtonsUpperUp), area.GetButton(Bedroom.RollerShutterButtonsUpperDown), _messageBroker);

            area.GetRollerShutter(Bedroom.RollerShutterRight)
                .ConnectWith(area.GetButton(Bedroom.RollerShutterButtonsLowerUp), area.GetButton(Bedroom.RollerShutterButtonsLowerDown), _messageBroker);

            var ceilingLights = _actuatorFactory.RegisterLogicalComponent(area, Bedroom.CombinedCeilingLights)
                .WithComponent(area.GetLamp(Bedroom.LightCeilingWall))
                .WithComponent(area.GetLamp(Bedroom.LightCeilingWindow));

            area.GetButton(Bedroom.ButtonDoor).CreatePressedShortTrigger(_messageBroker).Attach(() => ceilingLights.TryTogglePowerState());
            area.GetButton(Bedroom.ButtonWindowUpper).CreatePressedShortTrigger(_messageBroker).Attach(() => ceilingLights.TryTogglePowerState());
            
            area.GetButton(Bedroom.ButtonDoor).CreatePressedLongTrigger(_messageBroker).Attach(() =>
            {
                area.GetComponent(Bedroom.LampBedLeft).TryTurnOff();
                area.GetComponent(Bedroom.LampBedRight).TryTurnOff();
                area.GetComponent(Bedroom.CombinedCeilingLights).TryTurnOff();
            });

            _automationFactory.RegisterRollerShutterAutomation(area, Bedroom.RollerShutterLeftAutomation)
                .WithRollerShutters(area.GetRollerShutter(Bedroom.RollerShutterLeft));

            _automationFactory.RegisterRollerShutterAutomation(area, Bedroom.RollerShutterRightAutomation)
                .WithRollerShutters(area.GetRollerShutter(Bedroom.RollerShutterRight));

            _automationFactory.RegisterTurnOnAndOffAutomation(area, Bedroom.LightCeilingAutomation)
                .WithTrigger(area.GetMotionDetector(Bedroom.MotionDetector))
                .WithTarget(area.GetComponent(Bedroom.LightCeiling))
                .WithTurnOnIfAllRollerShuttersClosed(area.GetRollerShutter(Bedroom.RollerShutterLeft), area.GetRollerShutter(Bedroom.RollerShutterRight))
                .WithEnabledAtNight()
                .WithSkipIfAnyIsAlreadyOn(area.GetLamp(Bedroom.LampBedLeft), area.GetLamp(Bedroom.LampBedRight));

            var fan = _actuatorFactory.RegisterFan(area, Bedroom.Fan, new BedroomFanAdapter(hsrel8));
            area.GetButton(Bedroom.ButtonWindowLower).CreatePressedShortTrigger(_messageBroker).Attach(() => fan.TryIncreaseLevel());

            area.GetButton(Bedroom.ButtonBedLeftInner).CreatePressedShortTrigger(_messageBroker).Attach(() => area.GetComponent(Bedroom.LampBedLeft).TryTogglePowerState());
            area.GetButton(Bedroom.ButtonBedLeftInner).CreatePressedLongTrigger(_messageBroker).Attach(() => area.GetComponent(Bedroom.CombinedCeilingLights).TryTogglePowerState());
            area.GetButton(Bedroom.ButtonBedLeftOuter).CreatePressedShortTrigger(_messageBroker).Attach(() => area.GetComponent(Bedroom.Fan).TryIncreaseLevel());
            area.GetButton(Bedroom.ButtonBedLeftOuter).CreatePressedLongTrigger(_messageBroker).Attach(() => area.GetComponent(Bedroom.Fan).TryTurnOff());

            area.GetButton(Bedroom.ButtonBedRightInner).CreatePressedShortTrigger(_messageBroker).Attach(() => area.GetComponent(Bedroom.LampBedRight).TryTogglePowerState());
            area.GetButton(Bedroom.ButtonBedRightInner).CreatePressedLongTrigger(_messageBroker).Attach(() => area.GetComponent(Bedroom.CombinedCeilingLights).TryTogglePowerState());
            area.GetButton(Bedroom.ButtonBedRightOuter).CreatePressedShortTrigger(_messageBroker).Attach(() => area.GetComponent(Bedroom.Fan).TryIncreaseLevel());
            area.GetButton(Bedroom.ButtonBedRightOuter).CreatePressedLongTrigger(_messageBroker).Attach(() => area.GetComponent(Bedroom.Fan).TryTurnOff());
        }

        private class BedroomFanAdapter : IFanAdapter
        {
            private readonly IBinaryOutput _relay0;
            private readonly IBinaryOutput _relay1;
            private readonly IBinaryOutput _relay2;

            public int MaxLevel { get; } = 3;

            public BedroomFanAdapter(HSREL8 hsrel8)
            {
                _relay0 = hsrel8[HSREL8Pin.Relay0];
                _relay1 = hsrel8[HSREL8Pin.Relay1];
                _relay2 = hsrel8[HSREL8Pin.Relay2];
            }

            public void SetState(int level, params IHardwareParameter[] parameters)
            {
                switch (level)
                {
                    case 0:
                        {
                            _relay0.Write(BinaryState.Low);
                            _relay1.Write(BinaryState.Low);
                            _relay2.Write(BinaryState.Low);
                            break;
                        }

                    case 1:
                        {
                            _relay0.Write(BinaryState.High);
                            _relay1.Write(BinaryState.Low);
                            _relay2.Write(BinaryState.High);
                            break;
                        }

                    case 2:
                        {
                            _relay0.Write(BinaryState.High);
                            _relay1.Write(BinaryState.High);
                            _relay2.Write(BinaryState.Low);
                            break;
                        }

                    case 3:
                        {
                            _relay0.Write(BinaryState.High);
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

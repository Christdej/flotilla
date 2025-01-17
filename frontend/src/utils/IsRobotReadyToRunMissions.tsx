import { useLanguageContext } from 'components/Contexts/LanguageContext'
import { Robot, RobotFlotillaStatus } from 'models/Robot'

export const isBatteryTooLow = (robot: Robot): boolean => {
    if (robot.batteryLevel === undefined || robot.batteryLevel === null) return false

    if (robot.model.batteryWarningThreshold && robot.batteryLevel < robot.model.batteryWarningThreshold) {
        return true
    } else if (
        robot.model.batteryMissionStartThreshold &&
        robot.batteryLevel < robot.model.batteryMissionStartThreshold &&
        robot.flotillaStatus === RobotFlotillaStatus.Recharging
    ) {
        return true
    }
    return false
}

export const isRobotPressureTooHigh = (robot: Robot): boolean => {
    if (robot.model.upperPressureWarningThreshold && robot.pressureLevel) {
        return robot.pressureLevel > robot.model.upperPressureWarningThreshold
    }
    return false
}

export const isRobotPressureTooLow = (robot: Robot): boolean => {
    if (robot.model.lowerPressureWarningThreshold && robot.pressureLevel) {
        return robot.pressureLevel < robot.model.lowerPressureWarningThreshold
    }
    return false
}

export const getNoMissionReason = (robot: Robot): string | undefined => {
    const { TranslateText } = useLanguageContext()

    if (isBatteryTooLow(robot)) {
        return robot.model.batteryMissionStartThreshold
            ? TranslateText(
                  'Battery is too low to start a mission. Queued missions will start when the battery is over {0}%.',
                  [robot.model.batteryMissionStartThreshold.toString()]
              )
            : TranslateText('Battery is too low to start a mission.')
    } else if (isRobotPressureTooHigh(robot)) {
        return robot.model.upperPressureWarningThreshold
            ? TranslateText(
                  'Pressure is too high to start a mission. Queued missions will start when the pressure is under {0}mBar.',
                  [(robot.model.upperPressureWarningThreshold * 1000).toString()]
              )
            : TranslateText('Pressure is too high to start a mission.')
    } else if (isRobotPressureTooLow(robot)) {
        return robot.model.lowerPressureWarningThreshold
            ? TranslateText(
                  'Pressure is too low to start a mission. Queued missions will start when the pressure is over {0}mBar.',
                  [(robot.model.lowerPressureWarningThreshold * 1000).toString()]
              )
            : TranslateText('Pressure is too low to start a mission.')
    }
    return undefined
}

import { Card, Typography } from '@equinor/eds-core-react'
import styled from 'styled-components'
import { DeckMissionCount, DeckMissionType, Inspection } from './InspectionSection'
import { getDeadlineInDays } from 'utils/StringFormatting'
import { tokens } from '@equinor/eds-tokens'
import { useLanguageContext } from 'components/Contexts/LanguageContext'

export const StyledDict = {
    Card: styled(Card)`
        display: flex;
        min-height: 150px;
        padding: 16px;
        flex-direction: column;
        justify-content: space-between;
        flex: 1 0 0;
        cursor: pointer;
        border-radius: 0px 4px 4px 0px;
    `,
    CardComponent: styled.div`
        display: flex;
        padding-right: 16px;
        justify-content: flex-end;
        gap: 10px;
        width: 100%;
    `,
    DeckCards: styled.div`
        display: grid;
        grid-template-columns: repeat(auto-fill, 450px);
        gap: 24px;
    `,
    DeckText: styled.div`
        display: grid;
        grid-template-rows: 25px 35px;
        align-self: stretch;
    `,
    TopDeckText: styled.div`
        display: flex;
        justify-content: space-between;
        margin-right: 5px;
    `,
    Rectangle: styled.div`
        display: flex-start;
        width: 24px;
        height: 100%;
        border-radius: 6px 0px 0px 6px;
    `,
    DeckCard: styled.div`
        display: flex;
        min-width: 400px;
        max-width: 450px;
        flex: 1 0 0;
        border-radius: 6px;
        min-height: 180px;
        box-shadow:
            0px 3px 4px 0px rgba(0, 0, 0, 0.12),
            0px 2px 4px 0px rgba(0, 0, 0, 0.14);
    `,
    Circle: styled.div`
        width: 13px;
        height: 13px;
        border-radius: 50px;
    `,
    MissionComponents: styled.div`
        display: flex;
        flex-direction: row;
        align-items: center;
        gap: 4px;
    `,
    DeckOverview: styled.div`
        display: flex;
        flex-direction: column;
        gap: 25px;
    `,
    MissionInspections: styled.div`
        display: flex;
        flex-direction: column;
        gap: 2px;
    `,
    Placeholder: styled.div`
        padding: 24px;
        border: 1px solid #dcdcdc;
        border-radius: 4px;
    `,
    Content: styled.div`
        display: flex;
        align-items: centre;
        gap: 5px;
    `,
}

export const getDeadlineInspection = (deadline: Date) => {
    const deadlineDays = getDeadlineInDays(deadline)
    switch (true) {
        case deadlineDays <= 1:
            return 'red'
        case deadlineDays > 1 && deadlineDays <= 7:
            return 'red'
        case deadlineDays > 7 && deadlineDays <= 14:
            return 'orange'
        case deadlineDays > 7 && deadlineDays <= 30:
            return 'green'
    }
    return 'green'
}

export const compareInspections = (i1: Inspection, i2: Inspection) => {
    if (!i1.missionDefinition.inspectionFrequency) return 1
    if (!i2.missionDefinition.inspectionFrequency) return -1
    if (!i1.missionDefinition.lastRun) return -1
    if (!i2.missionDefinition.lastRun) return 1
    else return i1.deadline!.getTime() - i2.deadline!.getTime()
}

interface ICardMissionInformationProps {
    deckId: string
    deckMissions: DeckMissionType
}

export function CardMissionInformation({ deckId, deckMissions }: ICardMissionInformationProps) {
    const { TranslateText } = useLanguageContext()

    var colorsCount: DeckMissionCount = {
        red: { count: 0, message: 'Must be inspected this week' },
        orange: { count: 0, message: 'Must be inspected within two weeks' },
        green: { count: 0, message: 'Up to date' },
        grey: { count: 0, message: '' },
    }

    deckMissions[deckId].inspections.forEach((inspection) => {
        if (!inspection.deadline) {
            if (!inspection.missionDefinition.lastRun && inspection.missionDefinition.inspectionFrequency) {
                colorsCount['red'].count++
            } else {
                colorsCount['green'].count++
            }
        } else {
            const dealineColor = getDeadlineInspection(inspection.deadline)
            colorsCount[dealineColor!].count++
        }
    })

    return (
        <StyledDict.MissionInspections>
            {Object.keys(colorsCount)
                .filter((color) => colorsCount[color].count > 0)
                .map((color) => (
                    <StyledDict.MissionComponents key={color}>
                        <StyledDict.Circle style={{ background: color }} />
                        <Typography color={tokens.colors.text.static_icons__secondary.rgba}>
                            {colorsCount[color].count > 1 &&
                                colorsCount[color].count +
                                    ' ' +
                                    TranslateText('Missions').toLowerCase() +
                                    ' ' +
                                    TranslateText(colorsCount[color].message).toLowerCase()}
                            {colorsCount[color].count === 1 &&
                                colorsCount[color].count +
                                    ' ' +
                                    TranslateText('Mission').toLowerCase() +
                                    ' ' +
                                    TranslateText(colorsCount[color].message).toLowerCase()}
                        </Typography>
                    </StyledDict.MissionComponents>
                ))}
        </StyledDict.MissionInspections>
    )
}
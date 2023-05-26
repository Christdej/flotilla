import { Typography } from '@equinor/eds-core-react'
import { TranslateText } from 'components/Contexts/LanguageContext'
import { Robot } from 'models/Robot'
import { LocalizationDialog } from './LocalizationDialog'

interface RobotProps {
    robot: Robot
}

export function LocalizationSection({ robot }: RobotProps) {
    const [selectedAssetDeck, setSelectedAssetDeck] = useState<AssetDeck>()
    const [assetDecks, setAssetDecks] = useState<AssetDeck[]>()

    useEffect(() => {
        BackendAPICaller.getAssetDecks().then((response: AssetDeck[]) => {
            setAssetDecks(response)
        })
    }, [])

    const getAssetDeckNames = (assetDecks: AssetDeck[]): Map<string, AssetDeck> => {
        var assetDeckNameMap = new Map<string, AssetDeck>()
        assetDecks.map((assetDeck: AssetDeck) => {
            assetDeckNameMap.set(assetDeck.deckName, assetDeck)
        })
        return assetDeckNameMap
    }
    const assetDeckNames = assetDecks !== undefined ? Array.from(getAssetDeckNames(assetDecks).keys()).sort() : []

    const onSelectedDeck = (changes: AutocompleteChanges<string>) => {
        const selectedDeckName = changes.selectedItems[0]
        const selectedAssetDeck = assetDecks?.find((assetDeck) => assetDeck.deckName === selectedDeckName)
        setSelectedAssetDeck(selectedAssetDeck)
    }

    const onClickLocalize = () => {
        if (selectedAssetDeck) {
            BackendAPICaller.postLocalizationMission(
                selectedAssetDeck?.defaultLocalizationPose,
                robot.id,
                selectedAssetDeck.id
            )
        }
    }
    return (
        <>
            <Typography variant="h2">{TranslateText('Localization')}</Typography>
            <LocalizationDialog robot={robot} />
        </>
    )
}

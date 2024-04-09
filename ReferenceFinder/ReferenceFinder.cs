using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using ResoniteHotReloadLib;
using ResoniteModLoader;
using System.Collections.Generic;

namespace ReferenceFinder
{
	public class ReferenceFinder : ResoniteMod
	{
		public override string Name => "Reference Finder Wizard";
		public override string Author => "Nytra";
		public override string Version => "1.0.0";
		public override string Link => "https://github.com/Nytra/ResoniteReferenceFinderWizard";

		const string WIZARD_TITLE = "Reference Finder Wizard (Mod)";

		public override void OnEngineInit()
		{
			HotReloader.RegisterForHotReload(this);
			Engine.Current.RunPostInit(AddMenuOption);
		}
		static void AddMenuOption()
		{
			DevCreateNewForm.AddAction("Editor", WIZARD_TITLE, (x) => ReferenceFinderWizard.GetOrCreateWizard(x));
		}

		static void BeforeHotReload()
		{
			HotReloader.RemoveMenuOption("Editor", WIZARD_TITLE);
		}

		static void OnHotReload(ResoniteMod modInstance)
		{
			AddMenuOption();
		}

		class ReferenceFinderWizard
		{
			public static ReferenceFinderWizard GetOrCreateWizard(Slot x)
			{
				return new ReferenceFinderWizard(x);
			}
			Slot WizardSlot;

			//eadonly ReferenceField<Slot> searchRoot;
			readonly ReferenceField<IWorldElement> elementField;

			readonly ValueField<bool> processWorkerSyncMembers;
			readonly ValueField<bool> processContainedComponents;
			readonly ValueField<bool> processChildrenSlots;
			readonly ValueField<bool> ignoreNonPersistent;
			readonly ValueField<bool> ignoreSelfReferences;
			readonly ValueField<bool> ignoreSlotParentRef;
			readonly ValueField<bool> showDetails;
			//readonly ValueField<bool> confirmDestroy;
			//readonly ValueField<string> nameField;
			//readonly ValueField<bool> matchCase;
			//readonly ValueField<bool> allowChanges;
			//readonly ValueField<bool> searchNiceName;
			readonly ValueField<int> maxResults;
			//readonly ValueField<bool> exactMatch;

			readonly ReferenceMultiplexer<ISyncRef> results;

			//readonly Button destroyButton;
			readonly Button searchButton;
			//readonly Button enableButton;
			//readonly Button disableButton;

			bool performingOperations = false;

			readonly Text statusText;
			void UpdateStatusText(string info)
			{
				statusText.Content.Value = info;
			}

			string GetSlotParentHierarchyString(Slot slot, bool reverse = true)
			{
				string str;
				List<Slot> parents = new List<Slot>();

				slot.ForeachParent((parent) =>
				{
					parents.Add(parent);
				});

				if (reverse)
				{
					str = "";
					parents.Reverse();
					bool first = true;
					foreach (Slot s in parents)
					{
						if (first)
						{
							str += s.Name;
							first = false;
						}
						else
						{
							str += "/" + s.Name;
						}
					}
					if (first)
					{
						str += slot.Name;
						first = false;
					}
					else
					{
						str += "/" + slot.Name;
					}

				}
				else
				{
					str = slot.Name;
					foreach (Slot s in parents)
					{
						str += "/" + s.Name;
					}
				}

				return str;
			}

			bool ValidateWizard()
			{
				//if (searchRoot.Reference.Target == null)
				//{
				//	UpdateStatusText("No search root provided!");
				//	return false;
				//}

				//if (componentField.Reference.Target == null)
				//{
				//UpdateStatusText("No component provided!");
				//return false;
				//}

				if (elementField.Reference.Target == null)
				{
					UpdateStatusText("No element provided!");
					return false;
				}

				//if (processWorkerSyncMembers.Value && elementField.Reference.Target is not Worker)
				//{
				//	UpdateStatusText("Provided element is not a Worker type!");
				//	return false;
				//}

				if (performingOperations)
				{
					UpdateStatusText("Operations in progress! (Or the mod has crashed)");
					return false;
				}

				return true;
			}

			//bool IsComponentMatch(Component c)
			//{
			//	bool matchType, matchName;
			//	string compName, searchString;

			//	if (ignoreGenericTypes.Value.Value)
			//	{
			//		matchType = c.GetType().Name == componentField.Reference.Target?.GetType().Name;
			//	}
			//	else
			//	{
			//		matchType = c.GetType() == componentField.Reference.Target?.GetType();
			//	}

			//	compName = searchNiceName.Value.Value ? c.GetType().GetNiceName() : c.GetType().Name;
			//	compName = matchCase.Value.Value ? compName : compName.ToLower();

			//	searchString = matchCase.Value.Value ? nameField.Value.Value : nameField.Value.Value?.ToLower();

			//	matchName = searchString != null &&
			//				searchString.Trim() != "" &&
			//				(exactMatch.Value.Value ? compName == searchString.Trim() : compName.Contains(searchString.Trim()));

			//	if (componentField.Reference.Target == null || nameField.Value.Value == null || nameField.Value.Value.Trim() == "")
			//	{
			//		return matchType || matchName;
			//	}
			//	else
			//	{
			//		return matchType && matchName;
			//	}
			//}

			//List<Component> GetSearchComponents()
			//{
			//	return searchRoot.Reference.Target?.GetComponentsInChildren((Component c) => IsComponentMatch(c));
			//}

			ReferenceFinderWizard(Slot x)
			{
				WizardSlot = x;
				WizardSlot.Tag = "Developer";
				WizardSlot.PersistentSelf = false;
				WizardSlot.LocalScale *= 0.0008f;

				Slot Data = WizardSlot.AddSlot("Data");
				//searchRoot = Data.AddSlot("searchRoot").AttachComponent<ReferenceField<Slot>>();
				//searchRoot.Reference.Value = WizardSlot.World.RootSlot.ReferenceID;
				elementField = Data.AddSlot("elementField").AttachComponent<ReferenceField<IWorldElement>>();
				processWorkerSyncMembers = Data.AddSlot("processWorkerSyncMembers").AttachComponent<ValueField<bool>>();
				processWorkerSyncMembers.Value.Value = true;
				processContainedComponents = Data.AddSlot("processContainedComponents").AttachComponent<ValueField<bool>>();
				processContainedComponents.Value.Value = true;
				processChildrenSlots = Data.AddSlot("processChildrenSlots").AttachComponent<ValueField<bool>>();
				processChildrenSlots.Value.Value = true;
				ignoreNonPersistent = Data.AddSlot("ignoreNonPersistent").AttachComponent<ValueField<bool>>();
				ignoreSelfReferences = Data.AddSlot("ignoreSelfReferences").AttachComponent<ValueField<bool>>();
				ignoreSelfReferences.Value.Value = true;
				ignoreSlotParentRef = Data.AddSlot("ignoreSlotParentRef").AttachComponent<ValueField<bool>>();
				//ignoreGenericTypes = Data.AddSlot("ignoreGenericTypes").AttachComponent<ValueField<bool>>();
				showDetails = Data.AddSlot("showDetails").AttachComponent<ValueField<bool>>();
				//confirmDestroy = Data.AddSlot("confirmDestroy").AttachComponent<ValueField<bool>>();
				//nameField = Data.AddSlot("nameField").AttachComponent<ValueField<string>>();
				//matchCase = Data.AddSlot("matchCase").AttachComponent<ValueField<bool>>();
				//allowChanges = Data.AddSlot("allowChanges").AttachComponent<ValueField<bool>>();
				//searchNiceName = Data.AddSlot("searchNiceName").AttachComponent<ValueField<bool>>();
				maxResults = Data.AddSlot("maxResults").AttachComponent<ValueField<int>>();
				maxResults.Value.Value = 256;
				results = Data.AddSlot("results").AttachComponent<ReferenceMultiplexer<ISyncRef>>();
				//exactMatch = Data.AddSlot("exactMatch").AttachComponent<ValueField<bool>>();

				UIBuilder UI = RadiantUI_Panel.SetupPanel(WizardSlot, WIZARD_TITLE.AsLocaleKey(), new float2(800f, 756f));
				RadiantUI_Constants.SetupEditorStyle(UI);

				UI.Canvas.MarkDeveloper();
				UI.Canvas.AcceptPhysicalTouch.Value = false;

				UI.SplitHorizontally(0.5f, out RectTransform left, out RectTransform right);

				left.OffsetMax.Value = new float2(-2f);
				right.OffsetMin.Value = new float2(2f);

				UI.NestInto(left);

				VerticalLayout verticalLayout = UI.VerticalLayout(4f, childAlignment: Alignment.TopCenter);
				verticalLayout.ForceExpandHeight.Value = false;

				UI.Style.MinHeight = 24f;
				UI.Style.PreferredHeight = 24f;
				UI.Style.PreferredWidth = 400f;
				UI.Style.MinWidth = 400f;

				//UI.Text("Search Root:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
				//UI.Next("Root");
				//UI.Current.AttachComponent<RefEditor>().Setup(searchRoot.Reference);

				//UI.Spacer(24f);

				UI.Text("Element:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
				UI.Next("Element");
				UI.Current.AttachComponent<RefEditor>().Setup(elementField.Reference);

				UI.HorizontalElementWithLabel("Process children slots:", 0.942f, () => UI.BooleanMemberEditor(processChildrenSlots.Value));

				UI.HorizontalElementWithLabel("Process contained components:", 0.942f, () => UI.BooleanMemberEditor(processContainedComponents.Value));

				UI.HorizontalElementWithLabel("Process sync members:", 0.942f, () => UI.BooleanMemberEditor(processWorkerSyncMembers.Value));

				UI.HorizontalElementWithLabel("Ignore references contained within self:", 0.942f, () => UI.BooleanMemberEditor(ignoreSelfReferences.Value));

				UI.HorizontalElementWithLabel("Ignore slot parent references:", 0.942f, () => UI.BooleanMemberEditor(ignoreSlotParentRef.Value));

				UI.HorizontalElementWithLabel("Ignore non persistent references:", 0.942f, () => UI.BooleanMemberEditor(ignoreNonPersistent.Value));

				UI.Spacer(24f);

				//UI.Text("{B} Name Contains:").HorizontalAlign.Value = TextHorizontalAlignment.Left;

				//var textField = UI.TextField();
				//textField.Text.Content.OnValueChange += (field) => nameField.Value.Value = field.Value;

				//UI.HorizontalElementWithLabel("Search Nice Name (With type arguments):", 0.942f, () => UI.BooleanMemberEditor(searchNiceName.Value));
				//UI.HorizontalElementWithLabel("Match Case:", 0.942f, () => UI.BooleanMemberEditor(matchCase.Value));
				//UI.HorizontalElementWithLabel("Exact Match:", 0.942f, () => UI.BooleanMemberEditor(exactMatch.Value));

				//UI.Spacer(24f);

				UI.HorizontalElementWithLabel("Max Results:", 0.884f, () =>
				{
					var intField = UI.IntegerField(1, 1025);
					intField.ParsedValue.Value = maxResults.Value.Value;
					intField.ParsedValue.OnValueChange += (field) => maxResults.Value.Value = field.Value;
					return intField;
				});

				UI.HorizontalElementWithLabel("Spawn Detail Text:", 0.942f, () => UI.BooleanMemberEditor(showDetails.Value));

				searchButton = UI.Button("Search");
				searchButton.LocalPressed += SearchPressed;

				//UI.Text("----------");

				//UI.HorizontalElementWithLabel("Allow Changes:", 0.942f, () => UI.BooleanMemberEditor(allowChanges.Value));

				//enableButton = UI.Button("Enable");
				//enableButton.LocalPressed += EnablePressed;

				//disableButton = UI.Button("Disable");
				//disableButton.LocalPressed += DisablePressed;

				//UI.Spacer(24f);

				//UI.HorizontalElementWithLabel("Confirm Destroy:", 0.942f, () => UI.BooleanMemberEditor(confirmDestroy.Value));

				//destroyButton = UI.Button("Destroy");
				//destroyButton.LocalPressed += DestroyPressed;

				UI.Spacer(24f);

				UI.Text("Status:");
				statusText = UI.Text("---");

				UI.NestInto(right);
				UI.ScrollArea();
				UI.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);

				SyncMemberEditorBuilder.Build(results.References, "References", null, UI);

				WizardSlot.PositionInFrontOfUser(float3.Backward, distance: 1f);
			}

			bool isElementPersistent(IWorldElement element)
			{
				if (element.IsPersistent)
				{
					if (element.Parent != null)
					{
						return isElementPersistent(element.Parent);
					}
					else
					{
						return true;
					}
				}
				return false;
			}

			void SearchPressed(IButton button, ButtonEventData eventData)
			{
				if (!ValidateWizard()) return;

				performingOperations = true;
				searchButton.Enabled = false;

				results.References.Clear();

				var elements = new HashSet<IWorldElement>();
				GetSearchElements(elements, elementField.Reference.Target);
				FindReferences(elements, out bool stoppedEarly);

				if (stoppedEarly)
				{
					UpdateStatusText($"Found {results.References.Count} references (Max Results limit reached).");
				}
				else
				{
					UpdateStatusText($"Found {results.References.Count} references.");
				}

				if (showDetails.Value.Value && results.References.Count > 0)
				{
					Slot textSlot = WizardSlot.LocalUserSpace.AddSlot("Detail Text");
					UniversalImporter.SpawnText(textSlot, "Detail Text", GetAllText(), textSize: 12, canvasSize: new float2(1200, 400));
					textSlot.PositionInFrontOfUser();
				}

				performingOperations = false;
				searchButton.Enabled = true;
			}

			void GetSearchElements(HashSet<IWorldElement> elements, IWorldElement target)
			{
				elements.Add(target);
				if (processChildrenSlots.Value && target is Slot slot)
				{
					foreach (Slot childSlot in slot.Children)
					{
						GetSearchElements(elements, childSlot);
					}
				}
				if (processWorkerSyncMembers.Value && target is Worker worker)
				{
					foreach (ISyncMember syncMember in worker.SyncMembers)
					{
						GetSearchElements(elements, syncMember);
					}
				}
				if (processContainedComponents.Value && target is ContainerWorker<Component> containerWorker)
				{
					foreach (Component component in containerWorker.Components)
					{
						GetSearchElements(elements, component);
					}
				}
				else if (processContainedComponents.Value && target is ContainerWorker<UserComponent> containerWorker2)
				{
					foreach (UserComponent userComponent in containerWorker2.Components)
					{
						GetSearchElements(elements, userComponent);
					}
				}
			}

			void FindReferences(HashSet<IWorldElement> elements, out bool stoppedEarly)
			{
				bool stoppedEarlyInternal = false;
				WizardSlot.World.ForeachWorldElement((ISyncRef syncRef) =>
				{
					if (results.References.Count >= maxResults.Value.Value)
					{
						stoppedEarlyInternal = true;
						return;
					}
					if (elements.Contains(syncRef.Target)
						&& !syncRef.Target.IsLocalElement
						&& !syncRef.IsLocalElement
						&& syncRef != elementField.Reference
						&& syncRef.Parent != results.References
						&& !(ignoreNonPersistent.Value && !isElementPersistent(syncRef))
						&& !(ignoreSlotParentRef.Value && (syncRef.Parent is Slot s && s.ParentReference == syncRef))
						&& !(ignoreSelfReferences.Value && syncRef.IsChildOfElement(elementField.Reference.Target)))
					{
						results.References.Add(syncRef);
					}
				});
				stoppedEarly = stoppedEarlyInternal;
			}

			string GetAllText()
			{
				string text = "";
				foreach (ISyncRef element in results.References)
				{
					text += GetText(element);
				}
				return text;
			}

			string GetText(ISyncRef syncRef)
			{
				return GetElementText(syncRef.Target, showParent: true) + " referenced by " + GetElementText(syncRef, showParent: true) + " at " + $"<color=hero.cyan>{GetSlotParentHierarchyString(syncRef.FindNearestParent<Slot>())}</color>" + "\n";
			}

			string GetElementText(IWorldElement element, bool showType = false, bool showParent = false)
			{
				string typeText = $"<color=hero.yellow>{element.GetType().GetNiceName()}</color>";
				string nameText = $"<color=hero.green>{element.Name}</color>";
				string parentText = $"<color=hero.purple>{element.Parent.Name}</color>";
				//return (showType ? typeText + " " : "") + nameText + (showParent ? " under " + parentText : "");
				return (showType ? typeText + " " : "" + (showParent ? parentText + "." : "") + nameText);
			}
		}
	}
}